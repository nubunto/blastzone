# Blastzone

Blastzone is a platform fighter engine. It ships with a first-party game that proves the engine works, but the engine is the product.

## Structure

**Engine**:
The reusable platform-fighter runtime and tooling that any game is built on.
_Avoid_: Framework, core

**Game**:
A product built on the Engine, supplying characters, stages and content. The first-party Game exists to prove the Engine.
_Avoid_: Client, demo

## Time

**Tick**:
One fixed step of the Simulation. There are exactly 60 Ticks per second.
_Avoid_: Frame (reserve "frame" for rendering), update, step

**Simulation**:
The deterministic part of the Engine that advances the match one Tick at a time from inputs alone.
_Avoid_: Gameplay loop, logic

**Rollback**:
Restoring an earlier Simulation snapshot and re-simulating forward when a remote input arrives that differs from the prediction.
_Avoid_: Resim, rewind

## Match

**Match**:
One game session of 1 to 4 Players on one Stage, from the first Tick until a winner is decided.
_Avoid_: Game (that's the product), round, set

**Player**:
A seat in a Match, controlled by a person, the network or a replay, and driving exactly one Character.
_Avoid_: Fighter (when meaning the seat), user

**Character**:
A playable fighter, defined by its attributes and Move Definitions.
_Avoid_: Fighter, champion, hero

**Stage**:
The arena a Match is played on: its collision, platforms and Blast Zones.
_Avoid_: Map, level, arena

**Move Definition**:
The data describing one action a Character can perform, authored as a text file.
_Avoid_: Attack data, frame data (when meaning the file), moveset entry

## Modes

**Training Mode**:
A Match with debugging and practice tools enabled, such as hitbox display, frame stepping and state reset.
_Avoid_: Practice mode, lab

## Combat

**Hitbox**:
A circle or capsule attached to a Move Definition that damages any Hurtbox it overlaps.
_Avoid_: Hit area, attack box

**Hurtbox**:
A circle or capsule on a Character that Hitboxes can strike.
_Avoid_: Body box, vulnerable area

**Hit Group**:
A set of Hitboxes within one move that can hit a given target at most once.
_Avoid_: Hit ID, rehit group

**Clank**:
Two attacking Hitboxes colliding with similar damage, which cancels both attacks.
_Avoid_: Priority clash, cancel

**Trade**:
Two Players hitting each other on the same Tick, so both are hit.
_Avoid_: Clash, double hit

## Input

**Buffer Window**:
How many Ticks before an action becomes possible a pressed input is held, so it runs as soon as the action is allowed.
_Avoid_: Input leniency, queue

**Input Delay**:
The number of Ticks local input is held back during netplay to reduce Rollbacks.
_Avoid_: Lag, frame delay

**Shield**:
A defensive bubble with health that drains while held, blocks Hitboxes and breaks at zero.
_Avoid_: Block, guard

**Parry**:
A Shield raised with precise timing that rewards the defender. Each Game can turn it on or off.
_Avoid_: Perfect shield, just guard

**Grab**:
A Hitbox that beats a Shield and puts the target in the grabbed state.
_Avoid_: Catch, command grab (when meaning the generic one)

**Throw**:
A Move Definition used from the grabbed state, whose knockback comes from the thrower.
_Avoid_: Toss

**Pummel**:
A small attack on a grabbed target.
_Avoid_: Grab attack

**Timer**:
A countdown in Ticks, set in data, that resolves something without player mashing, such as a Grab escape.
_Avoid_: Mash meter, struggle

## Knockback Influence

**Landing Lag**:
The Ticks of recovery when a Character lands during an aerial action, set per move in data.
_Avoid_: Landing recovery

**Hitlag**:
The freeze applied to both attacker and target when a Hitbox connects.
_Avoid_: Hitpause, hitstop, freeze frames

**Hitstun**:
The Ticks after Hitlag during which a hit Character can't act.
_Avoid_: Stun, tumble

**DI**:
Directional Influence: holding a direction during Hitlag changes the launch angle.
_Avoid_: Influence, drift

**SDI**:
Smash Directional Influence: tapping a direction during Hitlag shifts the target's position.
_Avoid_: Shake, wiggle

**ASDI**:
Automatic SDI: a small shift in the held direction at the end of Hitlag.
_Avoid_: Auto-shift

**SSDI**:
Single-Shift Directional Influence: one larger shift of a set distance, triggered by a precisely timed stick input during Hitlag. Separate from ASDI.
_Avoid_: Double-stick DI

**LSI**:
Launch Speed Influence: holding up or down during Hitlag changes launch speed.
_Avoid_: Speed DI

**Tech**:
Pressing Shield as a launched Character hits a surface, which cancels the knockback.
_Avoid_: Break fall, ukemi

## Movement

**Dash Dance**:
Tapping the other way during a dash's initial Ticks, which turns the dash around.
_Avoid_: Foxtrot, dash back

**Teeter**:
Standing at a platform edge after walking or sliding slowly into it, instead of leaving it. Dashes, runs and fast slides still go off the edge. The speed threshold is a setting.
_Avoid_: Edge balance, wobble

**Jump Squat**:
The Ticks between pressing jump on the ground and leaving it. Releasing jump during it makes a Short Hop; holding it, a Full Hop.
_Avoid_: Jump startup, prejump

**Short Hop**:
A ground jump to the Character's short hop height. See Jump Squat.
_Avoid_: Small jump

**Full Hop**:
A ground jump to the Character's full hop height. See Jump Squat.
_Avoid_: Big jump

**Double Jump**:
A jump in the air. Each Character has a set number per airtime, restored on landing or grabbing a ledge.
_Avoid_: Air jump, midair jump

**Fast-fall**:
Tapping down at or after the peak of a jump to fall at the Character's fast-fall speed.
_Avoid_: Fast drop

**Drop-through Platform**:
A platform with only a top surface: it catches a falling Ground Probe, can be passed from below, and a down tap drops through it.
_Avoid_: Soft platform, pass-through platform

**Air Dodge**:
A dodge in the air with Intangibility. Its knobs (directional or neutral, velocity zeroed at the end, Helpless afterwards) are settings, so styles such as Melee, Ultimate or Rivals 2 are presets in data.
_Avoid_: Air roll

**Helpless**:
Airborne and unable to act until landing or grabbing a ledge, as after some Air Dodges.
_Avoid_: Special fall, freefall

**Wavedash**:
An Air Dodge into the ground right after leaving it, so the Character slides along it. Not a mechanic of its own: it falls out of the Air Dodge's velocity and traction.
_Avoid_: Wave dash

**Waveland**:
An Air Dodge into a platform from the air, sliding on landing. Same physics as a Wavedash.
_Avoid_: Wave land

**Wall Jump**:
Tapping toward a wall while airborne beside it to jump away from it. Each Character can or can't, in data.
_Avoid_: Wall kick

**Regrab**:
Grabbing a ledge again before standing on the Stage. Each Regrab gives less Intangibility; standing on the Stage resets the count.
_Avoid_: Ledge refresh

**Ledgedash**:
Dropping from a ledge and immediately jumping and Air Dodging onto the Stage. A setting lets leftover ledge Intangibility carry into the drop or jump for a number of Ticks.
_Avoid_: Ledge wavedash

## Stage Collision

**Collision Box**:
The axis-aligned rectangle a Character uses to collide with Stage walls and ceilings.
_Avoid_: ECB, environment box, pushbox (when meaning Stage collision)

**Ground Probe**:
The single point at the bottom-center of a Collision Box that decides grounding, slopes and edges.
_Avoid_: Foot point, floor sensor

## Match Flow

**Blast Zone**:
The axis-aligned rectangle around a Stage. A Character who crosses it is knocked out.
_Avoid_: Kill zone, death boundary, offstage

**Knockout**:
A Character crossing a Blast Zone, counted on the Tick the crossing happens.
_Avoid_: KO (fine in UI text), death, kill

**Stock**:
One of a Player's lives in a Match. A Knockout costs one Stock.
_Avoid_: Life, heart

**Replay**:
A recorded Match: the starting setup, engine version, Move Definition hashes and every Player's inputs. It plays back only on the matching engine version and data.
_Avoid_: Recording, VOD

**Frame Advantage**:
How many Ticks earlier one Character can act than the other after an interaction.
_Avoid_: Tick advantage (we keep the community term)

## Engine-Only Mechanics

These exist as mechanisms in the Engine but may be switched off in a given Game (ADR 0005).

**Floorhug**:
Staying grounded through a hit when ASDI pushes into the ground and the knockback is below a damage-scaled threshold. Present in the Engine, switched off in our Game.
_Avoid_: Crouch cancel (a related but separate mechanic)

## Ledge

**Intangibility**:
A state in which a Character's Hurtboxes can't be hit.
_Avoid_: Invincibility (reserved for being hit with no effect), i-frames

**Ledge Hog**:
The rule that only one Character can hold a given ledge at a time.
_Avoid_: Ledge occupancy, edge guard (that's the general tactic)

**Ledge Trump**:
Grabbing a ledge someone else is holding, which knocks the holder off.
_Avoid_: Ledge steal

## Entities

**Entity**:
Anything the Simulation tracks: a Character, Projectile, item or stage hazard.
_Avoid_: Object, actor, game object

**Projectile**:
An Entity spawned by a move that has its own Move Definitions and Hitboxes.
_Avoid_: Bullet, missile

**Respawn Platform**:
The temporary platform a Character reappears on after a Knockout.
_Avoid_: Revival platform, halo

**Armor**:
A move property that lets a Character take hits during set Ticks without entering Hitstun.
_Avoid_: Super armor (use Armor with a threshold), heavy armor

**Command Grab**:
A special move that grabs, usually beating Shield, separate from the standard Grab.
_Avoid_: Special grab
