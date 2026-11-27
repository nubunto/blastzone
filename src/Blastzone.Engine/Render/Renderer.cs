using Brutal.SdlApi;
using Brutal.Numerics;
using Brutal.ShaderCApi;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace Blastzone.Engine.Render;

/// <summary>
/// Minimal Vulkan renderer: clears to a solid color and draws one centered RGB triangle.
/// No depth test. Fixed-size window, so the swapchain is never rebuilt.
/// Adapted from BRUTAL's samples/Demo/DemoVulkan.cs.
/// </summary>
public sealed unsafe class Renderer : IDisposable
{
    public static readonly float4 ClearColor = new(0.1f, 0.1f, 0.1f, 1.0f);

    const string VertexGlsl = """
        #version 450
        layout(location = 0) out vec3 color;

        // Three corners of a centered triangle in clip space (Vulkan: +Y is down).
        const vec2 positions[3] = vec2[](vec2(0.0, -0.5), vec2(0.5, 0.5), vec2(-0.5, 0.5));
        const vec3 colors[3] = vec3[](vec3(1.0, 0.0, 0.0), vec3(0.0, 1.0, 0.0), vec3(0.0, 0.0, 1.0));

        void main()
        {
            gl_Position = vec4(positions[gl_VertexIndex], 0.0, 1.0);
            color = colors[gl_VertexIndex];
        }
        """;

    const string FragmentGlsl = """
        #version 450
        layout(location = 0) in vec3 color;
        layout(location = 0) out vec4 outColor;

        void main()
        {
            outColor = vec4(color, 1.0);
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

        _swapchain = _device.CreateSwapchainKHR(
            new VkSwapchainCreateInfoKHR
            {
                Surface = _surface,
                MinImageCount = imageCount,
                ImageFormat = format,
                ImageColorSpace = surfaceFormats[0].ColorSpace,
                ImageExtent = _extent,
                ImageArrayLayers = 1,
                ImageUsage = VkImageUsageFlags.ColorAttachmentBit,
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

        _pipelineLayout = _device.CreatePipelineLayout([], [], null);
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

    public void DrawFrame()
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
        _commandBuffer.Draw(3, 1, 0, 0);
        _commandBuffer.EndRendering();

        Transition(image, VkImageLayout.ColorAttachmentOptimal, VkImageLayout.PresentSrcKHR,
            VkPipelineStageFlags.ColorAttachmentOutputBit, VkPipelineStageFlags.BottomOfPipeBit,
            VkAccessFlags.ColorAttachmentWriteBit, VkAccessFlags.None);

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
