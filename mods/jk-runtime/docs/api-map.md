# Public API map

Choose a service by responsibility, then follow its guide before copying a
signature. `PublicApi.md` in the built SDK lists every reflected public type and
declared public/protected member, parameter defaults, enum values and available
type XML summaries. IDE XML also supplies member comments. The signature reference
is generated from that SDK's DLL, not a manually frozen list.
Inherited members are described by their declaring base type/framework.

| API family | Responsibility and guide |
| --- | --- |
| `UI.UiSounds`, `UI.UiSound`, `UI.UiTheme` | [Native feedback ownership and color roles](ui-pages.md#native-feedback-and-colors) |
| `RuntimeApi`, `ModuleDefinition`, `ModuleContext`, `ModuleStatus`, capabilities | [Lifecycle, dependency resolution and activation](lifecycle.md) |
| `Modules.*Attribute`, `PackageHost`, `UiServices` | [Packaging and discovery](api.md#packaging); [first package](getting-started.md) |
| `RuntimeScope`, `StartupMeasurement` | [Preparation and explicit resource ownership](preparation.md) |
| `PresentationScheduling`, `PresentationRequest`, `FrameComposition`, `ISceneCompositor`, `FrameStyle`, `OwnedPatches`, `NativePerformance` | [Runtime coordination](runtime-coordination.md) |
| `Input.SharedKeyboard`, `KeyboardSample`, `KeyboardSubscription`, `KeyboardActionEdges`, `Gameplay.PlayerControl`, `UI.TextEntryBuffer` | [Runtime coordination](runtime-coordination.md) |
| `NativeFrameDispatch` | Explicit native frame calls for pacing owners; [state and time](state-and-time.md) |
| `GameContract` | Native structural validation; [compatibility](compatibility.md), fingerprint is separate evidence |
| `RuntimeJournal`, `RuntimeNotice`, `RuntimeResources`, `PerformanceMeasurement` | [Diagnostics and outstanding resources](diagnostic-workflow.md) |
| `Settings.Setting<T>`, `Commands`, `SettingsFile<T>`, `SettingsReadStatus`, `NativeSaveFiles`, `AtomicXmlFile`, `DataMigration` | [Settings, scheduling and durable files](settings-and-commands.md) |
| `BackgroundWorkQueue`, `BackgroundWork`, `BackgroundWorkState`, `Gameplay.ComponentAttachment` | Bounded managed background work and owned native components; see [ownership](settings-and-commands.md#owned-work-and-components) |
| `MethodValidity`, `MethodValidityLease`, `Gameplay.Movement*`, `ContactEvidence`, `ContactPath`, `MaterialCapabilities`, `MaterialInfo`, `MaterialRegistry`, `SpeedCapability`, `Support*` | [Material contracts, observation and bounded tracing](interop.md) |
| `Gameplay.BodyPipeline`, `BodyPhase`, `JumpSlot`, `JumpNodeBindings` | [Movement insertion and controller ownership](api.md#gameplay-ordering) |
| `Gameplay.GameFeatures`, `IPlayerForm`, `IAirThrust`, `IMovementRules`, `MovementMode` | Cooperating controller roles; [shared services](api.md#shared-services) |
| `Gameplay.GameplayEvents`, event records/enums, `JumpEvents`, jump evidence/results | [Observed gameplay versus physical evidence](events-and-input.md) |
| `Gameplay.NativePause`, `AreaEntryObserver`, `ComponentSuspension` | [Pause, area entry and owned suspension](events-and-input.md) |
| `Gameplay.Mechanic*`, `PresentationActivity` | [On-demand inventory and suspended-physics presentation](geometry-and-mechanics.md) |
| `Gameplay.RunModifiers`, attribution records/enums | [Modified-run evidence](compatibility.md#modified-player-attribution) |
| `Geometry.*` | [Native geometry, actor profiles, block catalogue](geometry-and-mechanics.md) |
| `Input.ActionInputs`, `ActionFrame` | [Cached logical action state](events-and-input.md#logical-actions) |
| `Input.NativeInputFrames` | [Complete native pad publication and scoped menu dispatch](events-and-input.md#native-input-publication) |
| `Input.SharedActionSampler`, physical bindings/chords/transitions | [Shared physical input evidence](events-and-input.md#physical-evidence) |
| `Input.HighRateInputSampler`, DirectInput/XInput readers/interfaces, binding snapshots, diagnostics/log | Lower-level/compatibility input contracts; [input backends](events-and-input.md#backend-and-compatibility-apis) |
| `State.*` | [Participant snapshots, fields, attempt identity and clock leases](state-and-time.md) |
| `Simulation.*` | [Explicit isolated coverage, sessions and native ballistic adapter](simulation.md) |
| `UI.UIApi`, registrations/definitions, settings/bindings/chords | [UI reference](ui-api.md) |
| `UI.UiBindingsPage` | [Focused binding pages](ui-api.md#focused-binding-pages), using explicit IDs and existing providers |
| `UI.UiTextEntryPage`, `UiTextRenderer` | [Shared text entry](ui-pages.md#shared-text-entry) and page-owned name rendering |
| `UI.IUiPage`, `ScopedUiPage`, layouts, lists, pointer, theme, input hints, number controls | [Complete UI pages](ui-pages.md) |
| `UI.WorldInteraction*`, `WorldScreen`, merchants/currencies/inventory definitions | [World interaction](ui-api.md#world-interactions), [merchant and inventory reference](ui-api.md#merchant-interfaces) |
| `UI.ModEntry`, `UIApiSettings`, `CompactWorkshopGridsOption` | Runtime's native discovery/settings surface; use registration APIs rather than invoking its loader callbacks |

## Availability and versions

`Gameplay.MotionObservation`, `MotionObservationScope`, `MotionObserver`,
`MotionSample`, `MotionKind` and `MotionHandlerStatus` are described in
[motion observation](motion-observation.md). API 1.31 advertises
`motion-observation-v1`; availability does not promise coverage of every handler.

`UI.MenuTreeSession` coordinates native and presentation updates of one menu
tree lifetime. The native parent receives completion without rerunning the
completed tree. Keep the session when changing clocks; create a new one only
when the native owner starts a new tree lifetime. See [UI pages](ui-pages.md).

Since API 1.20, `Geometry.NativeWorldGeometry.TryReadNativeBounds` reads exact
native block fields without invoking block methods. Unknown/custom subclasses
return false, not an empty shape. This is a geometry observation, not permission
to simulate a block's gameplay effects or call its collision callbacks.

`RuntimeApi.Supports` and `UIApi.Supports` have different feature vocabularies.
They report supported API facilities, not whether a controller is enabled, a
device is connected or a third-party provider has registered. The generated
reference includes each supported feature string from the reviewed source.
Declare the package's minimum SDK/API version and query optional providers
through capabilities; never assume assembly identity `1.0.0.0` means API 1.0.

## Public does not mean automatic

Reading `Mechanics.Inspect()` invokes registered state readers. Reading live
geometry returns native object references inside copied arrays. Opening a
simulation session invokes capture callbacks. Creating a setting toggle reads
its getter. Opening/exporting diagnostics can collect a substantial inventory.
Call these deliberately in the correct phase; a read-only operation can still
be expensive. See [preparation](preparation.md#work-that-must-not-escape-loading).
