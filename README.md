# purenote

A small text editor for Windows.

The point of it is that it leaves files alone. If a file has no BOM, saving it
won't add one. If it uses LF, it stays LF. The status bar shows the encoding and
the line ending, so there are no surprises when you commit the result.

Encoding and line endings can be changed by hand, and it warns you when the new
one can't hold the text you have. Find and Replace can ignore case and accents,
or match exactly.

Shortcuts are Ctrl+N, Ctrl+O and Ctrl+S.

## Running it

You need .NET Framework 4.8, which Windows 10 (1903 and later) and Windows 11
already have.

## Building it

```
dotnet build -c Release
```

That gives you `publish\purenote.exe`, and that single file is the whole program.
ILRepack merges the dependencies into it during a Release build. The runtime is
not bundled, it uses the .NET Framework already on the machine.
