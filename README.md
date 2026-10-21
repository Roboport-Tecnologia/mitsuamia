# mitsuamia

C# bindings for [mitsuami](https://github.com/Roboport-Tecnologia/mitsuami):
apps written in .NET, shown with each platform's own controls. AppKit on
macOS, WinUI 3 on Windows, and GTK 4 or Kirigami on Linux.

Avalonia and WPF apps keep their view models; only their views change.
The first app ported is [SourceGit](https://github.com/Roboport-Tecnologia/sourcegit)
(its `mitsuami` branch).

A proof of concept so far: `docs/ARCHITECTURE.md` has the plan.

## Trying it

Build mitsuami's C ABI (`mitsuami-ffi`), point `MITSUAMI_FFI` at it, and
run a sample:

```sh
cargo build -p mitsuami-ffi --manifest-path ../rust-ui-toolkit/Cargo.toml
MITSUAMI_FFI=../rust-ui-toolkit/target/debug/libmitsuami_ffi.dylib dotnet run --project samples/Clone
```
