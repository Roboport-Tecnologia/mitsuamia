# mitsuamia

C# bindings for mitsuami. Three repositories work together:

- `../rust-ui-toolkit` (`mitsuami`): native always wins. It exports its core
  and built-in widgets over a C ABI.
- `../mitsuami-extras`: widgets some platforms have no native control for,
  with a C ABI of their own.
- This one: the C# layer over both, the MVVM bridge, and the XAML converter.

The port of SourceGit (`../sourcegit`, branch `mitsuami`, `upstream` is
sourcegit-scm's) is the stress test that drives all three.
`docs/ARCHITECTURE.md` has the design, the survey of SourceGit and the plan.

Follow mitsuami's `CLAUDE.md` for conventions: short, plain sentences that
say why; imperative commit subjects; generally useful findings go in the
docs.
