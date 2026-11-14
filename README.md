> **Moved to uno-lab.** This project now lives in the `matrix/` folder of [noknowscode/uno-lab](https://github.com/noknowscode/uno-lab/tree/main/matrix) (private). This repository is archived and kept for its history.

# Matrix Rain

A Matrix-style "digital rain" page transition. The characters scatter away from the mouse pointer, and the rain carries you between pages.

## Built with

- Uno Platform single project, `Uno.Sdk` 6.5.36 (pinned in `global.json`).
- Targets: Android, WebAssembly, Desktop (net10.0).
- Material theme, Hosting and dependency injection, Uno Toolkit, MVVM (CommunityToolkit), Kiota HTTP client, localization, Navigation extensions, Skia renderer.

## Run it

You need the .NET 10 SDK. Run `uno-check` first on a fresh machine.

```bash
dotnet run --project matrix/matrix.csproj -f net10.0-desktop
```

## Design docs

- [`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md)
- [`matrix-transition-implementation-brief.md`](matrix-transition-implementation-brief.md)

## License

MIT. See [LICENSE](LICENSE).
