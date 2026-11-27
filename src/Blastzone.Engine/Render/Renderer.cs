using Brutal;
using Brutal.SdlApi;
using Brutal.Numerics;
using Brutal.ShaderCApi;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace Blastzone.Engine.Render;

/// <summary>
/// Minimal Vulkan renderer for flat 2D shapes: each <see cref="Shape"/> is one quad drawn with push
/// constants (no vertex buffers), and the fragment shader cuts rectangles, capsules and outlines out of
/// it with signed distances. No depth test. Fixed-size window, so the swapchain is never rebuilt.
/// Adapted from BRUTAL's samples/Demo/DemoVulkan.cs.
/// </summary>
public sealed unsafe class Renderer : IDisposable
{
    public static readonly float4 ClearColor = new(0.1f, 0.1f, 0.1f, 1.0f);

    const string VertexGlsl = """
        #version 450
        layout(push_constant) uniform Shape
        {
            vec4 bounds;  // world-space quad: min.xy, max.xy
            vec4 segment; // capsule ends a.xy, b.xy (or rectangle min/max)
            vec4 color;
            vec4 params;  // x radius, y kind, z outline width
            vec4 camera;  // world to clip: scale.xy, offset.xy
        } shape;
        layout(location = 0) out vec2 world;

        const vec2 corners[6] = vec2[](vec2(0, 0), vec2(1, 0), vec2(1, 1), vec2(0, 0), vec2(1, 1), vec2(0, 1));

        void main()
        {
            world = mix(shape.bounds.xy, shape.bounds.zw, corners[gl_VertexIndex]);
            gl_Position = vec4(world * shape.camera.xy + shape.camera.zw, 0.0, 1.0);
        }
        """;

    const string FragmentGlsl = """
        #version 450
        layout(push_constant) uniform Shape
        {
            vec4 bounds;
            vec4 segment;
            vec4 color;
            vec4 params;
            vec4 camera;
        } shape;
        layout(location = 0) in vec2 world;
        layout(location = 0) out vec4 outColor;

        float capsuleDistance(vec2 p, vec2 a, vec2 b, float r)
        {
            vec2 pa = p - a, ba = b - a;
            float h = clamp(dot(pa, ba) / max(dot(ba, ba), 1e-6), 0.0, 1.0);
            return length(pa - ba * h) - r;
        }

        float rectDistance(vec2 p, vec2 lo, vec2 hi)
        {
            vec2 d = max(lo - p, p - hi);
            return max(d.x, d.y); // negative inside
        }

        void main()
        {
            int kind = int(shape.params.y + 0.5);
            float width = shape.params.z;
            float d = kind <= 1
                ? rectDistance(world, shape.segment.xy, shape.segment.zw)
                : capsuleDistance(world, shape.segment.xy, shape.segment.zw, shape.params.x);
            bool outline = kind == 1 || kind == 3;
            if (d > 0.0 || (outline && d < -width))
                discard;
            outColor = shape.color;
        }
        """;

    readonly Instance _instance;
    readonly DeviceEx _device;
    readonly VkSurfaceKHR _surface;
    readonly Queue _queue;
    VkSwapchainKHR _swapchain;
    readonly VkImage[] _images;
    readonly VkImageView[] _imageViews;
    readonly VkExtent2D _extent;
    readonly ShaderStages _shaderStages;
    readonly VkPipelineLayout _pipelineLayout;
    readonly VkPipeline _pipeline;
    readonly VkCommandPool _commandPool;
    readonly CommandBuffer _commandBuffer;
    readonly VkSemaphore _imageAvailable;
    readonly VkSemaphore[] _renderFinished;
    readonly VkFence _frameFence;
    readonly VkFormat _format;
    readonly PhysicalDevice _physicalDevice;
    readonly VkImageSubresourceRange _colorRange = new()
    {
        AspectMask = VkImageAspectFlags.ColorBit,
        LevelCount = 1,
        LayerCount = 1,
    };

    /// <summary>Call BEFORE SDL initializes video. On macOS, SDL won't search Homebrew's lib folder
    /// for the Vulkan loader, so point it there explicitly.</summary>
    public static void PrepareSdl()
    {
        const string homebrewLoader = "/opt/homebrew/lib/libvulkan.1.dylib";
        if (OperatingSystem.IsMacOS() && File.Exists(homebrewLoader))
            Sdl.PInvoke.SetHint("SDL_VULKAN_LIBRARY", homebrewLoader);
    }

    public Renderer(SdlWindow window)
    {
        var instanceExtensions = new HashSet<string>();
        VulkanHelpers.AddPlatformRequiredExtensions(instanceExtensions); // portability enumeration for MoltenVK
        foreach (var ext in Sdl.Vulkan.GetInstanceExtensions())
            instanceExtensions.Add(ext);

        _instance = Vulkan.CreateInstance(
            new Instance.CreateInfo
            {
                AppInfo = new VkApplicationInfo { ApiVersion = (int)VulkanHelpers.Api.VERSION_1_3 },
                Flags = VulkanHelpers.GetPlatformRequiredInstanceCreateFlags(),
                EnabledExtensions = instanceExtensions,
                EnabledLayers = [],
            }
        );

        _surface = new VkSurfaceKHR(Sdl.Vulkan.CreateSurface(window, (nint)_instance.VkHandle));

        PhysicalDevice? physicalDevice = null;
        var queueFamily = -1;
        foreach (var candidate in _instance.PhysicalDevices)
        {
            var families = new VkQueueFamilyProperties[candidate.GetQueueFamilyProperties_Count()];
            candidate.GetQueueFamilyProperties_Write(families);
            for (var i = 0; i < families.Length && physicalDevice is null; i++)
            {
                if ((families[i].QueueFlags & VkQueueFlags.GraphicsBit) != 0 && candidate.GetSurfaceSupportKHR(i, _surface))
                    (physicalDevice, queueFamily) = (candidate, i);
            }
        }
        if (physicalDevice is null)
            throw new InvalidOperationException("No Vulkan device can draw and present to the window");

        var deviceExtensions = new HashSet<string>();
        VulkanHelpers.AddSwapchainExtensions(deviceExtensions);
        VulkanHelpers.AddMandatoryIfSupportedDeviceExtensions(deviceExtensions, physicalDevice);

        var priority = 1.0f;
        var vulkan13Features = new VkPhysicalDeviceVulkan13Features { DynamicRendering = true };
        _device = physicalDevice.CreateDevice(
            new DeviceEx.CreateInfo
            {
                Extensions = deviceExtensions,
                QueueCreateInfos =
                [
                    new VkDeviceQueueCreateInfo
                    {
                        QueueFamilyIndex = queueFamily,
                        QueueCount = 1,
                        QueuePriorities = &priority,
                    },
                ],
                Next = &vulkan13Features,
            }
        );
        _queue = _device.GetQueue(queueFamily, 0);

        // Swapchain
        var capabilities = physicalDevice.GetSurfaceCapabilitiesKHR(_surface);
        _extent = capabilities.CurrentExtent;
        if (_extent.Width == -1 && _extent.Height == -1)
            _extent = new VkExtent2D(
                Math.Clamp(PixelSize(window).w, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width),
                Math.Clamp(PixelSize(window).h, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height)
            );

        var imageCount = Math.Max(capabilities.MinImageCount, 2);
        if (capabilities.MaxImageCount > 0)
            imageCount = Math.Min(imageCount, capabilities.MaxImageCount);

        VkCompositeAlphaFlagsKHR[] alphaModes =
        [
            VkCompositeAlphaFlagsKHR.OpaqueBitKHR,
            VkCompositeAlphaFlagsKHR.PreMultipliedBitKHR,
            VkCompositeAlphaFlagsKHR.PostMultipliedBitKHR,
            VkCompositeAlphaFlagsKHR.InheritBitKHR,
        ];
        var compositeAlpha = alphaModes.First(mode => (capabilities.SupportedCompositeAlpha & mode) != 0);

        var surfaceFormats = new VkSurfaceFormatKHR[physicalDevice.GetSurfaceFormatsKHR_Count(_surface)];
        physicalDevice.GetSurfaceFormatsKHR_Write(_surface, surfaceFormats);
        var format = surfaceFormats[0].Format;
        _format = format;
        _physicalDevice = physicalDevice;

        _swapchain = _device.CreateSwapchainKHR(
            new VkSwapchainCreateInfoKHR
            {
                Surface = _surface,
                MinImageCount = imageCount,
                ImageFormat = format,
                ImageColorSpace = surfaceFormats[0].ColorSpace,
                ImageExtent = _extent,
                ImageArrayLayers = 1,
                ImageUsage = VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.TransferSrcBit, // TransferSrc: screenshots
                ImageSharingMode = VkSharingMode.Exclusive,
                PreTransform = capabilities.CurrentTransform,
                CompositeAlpha = compositeAlpha,
                PresentMode = VkPresentModeKHR.FifoKHR,
                Clipped = true,
            },
            null
        );

        _images = new VkImage[_device.GetSwapchainImagesKHR_Count(_swapchain)];
        _device.GetSwapchainImagesKHR_Write(_swapchain, _images);
        _imageViews = new VkImageView[_images.Length];
        for (var i = 0; i < _images.Length; i++)
            _imageViews[i] = _device.CreateImageView(
                new VkImageViewCreateInfo
                {
                    Image = _images[i],
                    ViewType = VkImageViewType._2D,
                    Format = format,
                    SubresourceRange = _colorRange,
                },
                null
            );

        // Shaders, compiled from GLSL at startup.
        using var compiler = ShaderC.CreateCompiler();
        using var options = ShaderC.CreateCompilerOptions();
        VkShaderModule Compile(string glsl, ShaderKind kind)
        {
            using var spirv = compiler.Compile(System.Text.Encoding.UTF8.GetBytes(glsl), kind, "shader"u8, "main"u8, options);
            return ShaderModuleExtensions.FromSpirv(_device, new ReadOnlySpan<byte>(spirv.GetBytes(), (int)spirv.GetLength()), null);
        }
        _shaderStages = new ShaderStages(
            _device,
            [
                Presets.ShaderStage.Vertex(Compile(VertexGlsl, ShaderKind.VertexShader)),
                Presets.ShaderStage.Fragment(Compile(FragmentGlsl, ShaderKind.FragmentShader)),
            ],
            null
        );

        // Pipeline: no vertex input (positions live in the shader), no depth test.
        var vertexInput = new VkPipelineVertexInputStateCreateInfo();
        var viewport = new VkViewport { Width = _extent.Width, Height = _extent.Height, MaxDepth = 1 };
        var scissor = new VkRect2D(_extent);
        var viewportState = new VkPipelineViewportStateCreateInfo
        {
            ViewportCount = 1,
            Viewports = &viewport,
            ScissorCount = 1,
            Scissors = &scissor,
        };
        var rendering = new VkPipelineRenderingCreateInfo { ColorAttachmentCount = 1, ColorAttachmentFormats = &format };

        _pipelineLayout = _device.CreatePipelineLayout([],
            [new VkPushConstantRange
            {
                StageFlags = VkShaderStageFlags.VertexBit | VkShaderStageFlags.FragmentBit,
                Offset = (ByteSize32)0,
                Size = (ByteSize32)sizeof(ShapeConstants),
            }], null);
        _pipeline = _device.CreateGraphicsPipeline(
            VkPipelineCache.NullHandle,
            new VkGraphicsPipelineCreateInfo
            {
                Next = &rendering,
                StageCount = _shaderStages.Count,
                Stages = _shaderStages,
                VertexInputState = &vertexInput,
                InputAssemblyState = Presets.InputAssembly.TriangleList,
                ViewportState = &viewportState,
                RasterizationState = Presets.Rasterization.Fill.CullNone,
                MultisampleState = Presets.Multisample.None,
                ColorBlendState = Presets.BlendState.BlendNone,
                Layout = _pipelineLayout,
            },
            null
        );

        // Commands and sync: one frame in flight.
        _commandPool = _device.CreateCommandPool(
            new VkCommandPoolCreateInfo
            {
                QueueFamilyIndex = queueFamily,
                Flags = VkCommandPoolCreateFlags.ResetCommandBufferBit,
            },
            null
        );
        _commandBuffer = _device.AllocateCommandBuffer(
            new VkCommandBufferAllocateInfo { CommandPool = _commandPool, Level = VkCommandBufferLevel.Primary }
        );
        _imageAvailable = _device.CreateSemaphore(new VkSemaphoreCreateInfo(), null);
        _renderFinished = _images.Select(_ => _device.CreateSemaphore(new VkSemaphoreCreateInfo(), null)).ToArray();
        _frameFence = _device.CreateFence(VkFenceCreateFlags.SignaledBit, null);
    }

    static (int w, int h) PixelSize(SdlWindow window)
    {
        int w, h;
        Sdl.PInvoke.GetWindowSizeInPixels(window, &w, &h);
        return (w, h);
    }

    /// <summary>The window size in pixels, for fitting the camera.</summary>
    public (int Width, int Height) Extent => (_extent.Width, _extent.Height);

    /// <summary>
    /// Draws <paramref name="shapes"/> in order (later ones on top). When <paramref name="screenshotPath"/>
    /// is set, the finished frame is also saved there as a PNG.
    /// </summary>
    public void DrawFrame(Camera camera, ReadOnlySpan<Shape> shapes, string? screenshotPath = null)
    {
        _device.WaitForFence(_frameFence, -1);
        _device.ResetFences([_frameFence]);

        _device.AcquireNextImageKHR(_swapchain, -1, _imageAvailable, VkFence.NullHandle, out var imageIndex);
        var image = _images[imageIndex];

        _commandBuffer.Reset();
        _commandBuffer.Begin(VkCommandBufferUsageFlags.OneTimeSubmitBit);

        Transition(image, VkImageLayout.Undefined, VkImageLayout.ColorAttachmentOptimal,
            VkPipelineStageFlags.ColorAttachmentOutputBit, VkPipelineStageFlags.ColorAttachmentOutputBit,
            VkAccessFlags.None, VkAccessFlags.ColorAttachmentWriteBit);

        var colorAttachment = new VkRenderingAttachmentInfo
        {
            ImageView = _imageViews[imageIndex],
            ImageLayout = VkImageLayout.ColorAttachmentOptimal,
            LoadOp = VkAttachmentLoadOp.Clear,
            StoreOp = VkAttachmentStoreOp.Store,
            ClearValue = new VkClearColorValue { Float32 = ClearColor },
        };
        _commandBuffer.BeginRendering(
            new VkRenderingInfo
            {
                RenderArea = new VkRect2D(_extent),
                LayerCount = 1,
                ColorAttachmentCount = 1,
                ColorAttachments = &colorAttachment,
            }
        );
        _commandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, _pipeline);
        var aspect = _extent.Width / (float)_extent.Height;
        var scaleY = -1f / camera.HalfHeight; // Vulkan clip space: +Y is down
        var scaleX = 1f / (camera.HalfHeight * aspect);
        var cameraVector = new float4(scaleX, scaleY, -camera.CenterX * scaleX, -camera.CenterY * scaleY);
        foreach (var shape in shapes)
        {
            var constants = ShapeConstants.From(shape, cameraVector);
            _commandBuffer.PushConstants(_pipelineLayout, VkShaderStageFlags.VertexBit | VkShaderStageFlags.FragmentBit, (ByteSize)0, constants);
            _commandBuffer.Draw(6, 1, 0, 0);
        }
        _commandBuffer.EndRendering();

        Screenshot? screenshot = null;
        if (screenshotPath is not null)
        {
            screenshot = new Screenshot(this, screenshotPath);
            Transition(image, VkImageLayout.ColorAttachmentOptimal, VkImageLayout.TransferSrcOptimal,
                VkPipelineStageFlags.ColorAttachmentOutputBit, VkPipelineStageFlags.TransferBit,
                VkAccessFlags.ColorAttachmentWriteBit, VkAccessFlags.TransferReadBit);
            screenshot.RecordCopy(image);
            Transition(image, VkImageLayout.TransferSrcOptimal, VkImageLayout.PresentSrcKHR,
                VkPipelineStageFlags.TransferBit, VkPipelineStageFlags.BottomOfPipeBit,
                VkAccessFlags.TransferReadBit, VkAccessFlags.None);
        }
        else
        {
            Transition(image, VkImageLayout.ColorAttachmentOptimal, VkImageLayout.PresentSrcKHR,
                VkPipelineStageFlags.ColorAttachmentOutputBit, VkPipelineStageFlags.BottomOfPipeBit,
                VkAccessFlags.ColorAttachmentWriteBit, VkAccessFlags.None);
        }

        _commandBuffer.End();

        var signal = _renderFinished[imageIndex];
        _queue.Submit([_imageAvailable], [VkPipelineStageFlags.ColorAttachmentOutputBit], [_commandBuffer], [signal], _frameFence);

        var swapchain = _swapchain;
        _queue.PresentKHR(
            new VkPresentInfoKHR
            {
                WaitSemaphoreCount = 1,
                WaitSemaphores = &signal,
                SwapchainCount = 1,
                Swapchains = &swapchain,
                ImageIndices = &imageIndex,
            }
        );

        if (screenshot is not null)
        {
            _device.WaitForFence(_frameFence, -1);
            screenshot.SaveAndDispose();
        }
    }

    /// <summary>Host-visible copy of one swapchain image, written out as a PNG.</summary>
    sealed class Screenshot(Renderer renderer, string path)
    {
        VkBuffer _buffer;
        VkDeviceMemory _memory;
        int Width => renderer._extent.Width;
        int Height => renderer._extent.Height;

        public void RecordCopy(VkImage image)
        {
            var device = renderer._device;
            _buffer = device.CreateBuffer(new VkBufferCreateInfo
            {
                Size = (ByteSize64)(Width * Height * 4),
                Usage = VkBufferUsageFlags.TransferDstBit,
                SharingMode = VkSharingMode.Exclusive,
            }, null);
            _memory = device.AllocateMemory(renderer._physicalDevice, _buffer,
                VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit, null);
            device.BindBufferMemory(_buffer, _memory, (ByteSize64)0);
            renderer._commandBuffer.CopyImageToBuffer(image, VkImageLayout.TransferSrcOptimal, _buffer,
            [
                new VkBufferImageCopy
                {
                    ImageSubresource = new VkImageSubresourceLayers { AspectMask = VkImageAspectFlags.ColorBit, LayerCount = 1 },
                    ImageExtent = new VkExtent3D { Width = Width, Height = Height, Depth = 1 },
                },
            ]);
        }

        public void SaveAndDispose()
        {
            var device = renderer._device;
            var pixels = new byte[Width * Height * 4];
            // Plain vkMapMemory: MapMemory2 needs Vulkan 1.4 or VK_KHR_map_memory2, which MoltenVK may lack.
            var mapInfo = new MapInfo(_memory);
            var mapped = device.MapMemory(in mapInfo);
            new ReadOnlySpan<byte>(mapped.Pointer, pixels.Length).CopyTo(pixels);
            device.UnmapMemory(_memory);
            device.DestroyBuffer(_buffer, null);
            device.FreeMemory(_memory, null);

            var bgra = renderer._format.ToString().StartsWith("B8G8R8A8", StringComparison.OrdinalIgnoreCase);
            Png.WriteRgba(path, Width, Height, pixels, swapRedBlue: bgra);
        }
    }

    void Transition(VkImage image, VkImageLayout from, VkImageLayout to,
        VkPipelineStageFlags srcStage, VkPipelineStageFlags dstStage, VkAccessFlags srcAccess, VkAccessFlags dstAccess)
    {
        _commandBuffer.PipelineBarrier(srcStage, dstStage, VkDependencyFlags.None, [], [],
        [
            new VkImageMemoryBarrier
            {
                SrcAccessMask = srcAccess,
                DstAccessMask = dstAccess,
                OldLayout = from,
                NewLayout = to,
                SrcQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
                DstQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
                Image = image,
                SubresourceRange = _colorRange,
            },
        ]);
    }

    public void Dispose()
    {
        _device.WaitIdle();
        _device.DestroyFence(_frameFence, null);
        _device.DestroySemaphore(_imageAvailable, null);
        foreach (var s in _renderFinished)
            _device.DestroySemaphore(s, null);
        _device.DestroyCommandPool(_commandPool, null);
        _device.DestroyPipeline(_pipeline, null);
        _device.DestroyPipelineLayout(_pipelineLayout, null);
        _shaderStages.Dispose();
        foreach (var v in _imageViews)
            _device.DestroyImageView(v, null);
        _device.DestroySwapchainKHR(_swapchain, null);
        _instance.DestroySurfaceKHR(_surface, null);
        _device.Dispose();
        _instance.Dispose();
    }
}
