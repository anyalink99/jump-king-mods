# API compatibility policy

The assembly identity remains `JKRuntime, Version=1.0.0.0` for additive 1.x SDK
updates. The runtime/file version and `ApiMinor` identify available additions.
New packages require at least the minor version of the SDK packaging tool used
to build them. Old packages are not rewritten or forced to require new APIs.

Public type/member signatures are compatibility contracts. The build compares
them with the frozen 1.2, 1.3, 1.11, 1.24, 1.25 and 1.26 surfaces in
`tests/api-<version>.txt`, including protected
members, inheritance, implemented interfaces, generic constraints and enum
values. Removing an old member or changing its CLR signature fails the build.
This structural check does not prove equivalent behavior, optional-argument
source compatibility, arbitrary reflection code or all default values. Focused
native-game and consumer tests remain necessary.

`LegacyConsumer13` compiles against a frozen reference-only subset of 1.3, then
runs unchanged with the new DLL. The build checks every signature in that subset
against the frozen shipped surface before compiling the consumer. It exercises
real registration, state inspection and cleanup after binary replacement, not
just compiling an example against today's SDK. It is a focused compatibility
fixture, not an assertion about every old consumer or private reflection trick.

Game-thread services must be called and released on the game thread. A new
thread/lifetime guard may reject a previously unsafe call without changing its
signature. Cleanup should attempt unrelated releases after a failure and report
incomplete shared cleanup; it must not pretend that unsafe state was restored.

Geometry profiles and simulation schemas carry their own explicit versions.
Never change what an existing profile/version means to accommodate a different
actor. A new actor contour does not alter native physics. Diagnostic inventory
entries do not establish controller ownership or simulation support.

The simulation kernel remains a foundation for reviewed adapters, not a promise
of universal gameplay parity. Provider evidence and payload/schema versions are
explicit. Experimental adapters should use their own IDs/versions and document
their limits; they must not masquerade as a proven native adapter.

Internal classes and private game fields are not SDK contracts. Runtime adapters
validate required game fields/methods and fail visibly when unavailable. An
unchanged private signature is not proof that a game update preserved semantics.

Current overloads/default values are included in the generated standalone SDK
`PublicApi.md`. A type's presence in that reference is not a recommendation to
invoke native loader entry points directly. Use the [API map](api-map.md) and
the service's semantic guide. Updating signatures also requires updating the
[documentation and examples](maintaining-docs.md).
