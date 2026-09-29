# API compatibility policy

All additive 1.x SDK releases keep the assembly identity
`JKRuntime, Version=1.0.0.0`. Use the runtime or file version and `ApiMinor` to
check which additions are present. A newly built package requires at least the
SDK minor used by its packaging tool. Existing packages keep their own minimum;
Runtime does not rewrite them.

Public type and member signatures are compatibility contracts. The build checks
them against frozen 1.2, 1.3, 1.11, 1.24, 1.25 and 1.26 surfaces in
`tests/api-<version>.txt`. The comparison includes protected members,
inheritance, interfaces, generic constraints and enum values. Removing a member
or changing its CLR signature fails the build.

That check covers structure only. It cannot prove equivalent behavior,
optional-argument source compatibility, arbitrary reflection code or every
default value. Native-game and consumer tests still cover those behaviors.

`LegacyConsumer13` compiles against a frozen reference-only subset of 1.3, then
runs unchanged with the new DLL. The build checks every signature in that subset
against the frozen shipped surface before compiling the consumer. It exercises
real registration, state inspection and cleanup after binary replacement, not
just compiling an example against today's SDK. It is a focused compatibility
fixture, not an assertion about every old consumer or private reflection trick.

Game-thread services must be called and released on the game thread. A new
thread/lifetime guard may reject a previously unsafe call without changing its
signature. Cleanup should attempt unrelated releases after a failure and report
the resources that still require a restart.

Geometry profiles and simulation schemas carry their own explicit versions.
Never change what an existing profile/version means to accommodate a different
actor. A new actor contour does not alter native physics. Diagnostic inventory
entries do not establish controller ownership or simulation support.

The simulation kernel supports reviewed adapters with explicit coverage.
Provider evidence and payload/schema versions are explicit. Experimental adapters should use their own IDs/versions and document
their supported mechanics and validation status.

Internal classes and private game fields are not SDK contracts. Runtime adapters
validate required game fields/methods and fail visibly when unavailable. An
unchanged private signature is not proof that a game update preserved semantics.

Current overloads/default values are included in the generated standalone SDK
`PublicApi.md`. A type's presence in that reference is not a recommendation to
invoke native loader entry points directly. Use the [API map](api-map.md) and
the service's semantic guide. Updating signatures also requires updating the
[documentation and examples](maintaining-docs.md).
