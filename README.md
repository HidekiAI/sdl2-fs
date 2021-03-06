# SDL2 on F#
Pure dotnet-core (not .NET Framework 3 or 4), non-Mono depentedant F# sample code to interface with libSDL2

* SDL2 lib for the platforms of your choices can be found at https://www.libsdl.org/
* SDL2-CS (C# InterOp proxy/wrapper) can be found at: https://github.com/flibitijibibo/SDL2-CS
* Sample app to run is based off of https://www.willusher.io/sdl2%20tutorials/2013/08/17/lesson-1-hello-world

## Summary
* dotnet (without the .NET 4 Framework and/or mono)
* F# init call to libSDL2.so via wrapper
* Using SDL2-CS (SDL2 C#) from https://github.com/flibitijibibo/SDL2-CS (see notes)

## Prerequisites
* libSDL2 (please don't ask me about Windows or Mac), I will just assume you're here because you already are using it or have installed it

## Notes
* There are handful of NuGet that wraps SDL2; only tried one or two, but they want to be MonoGames or OpenTK friendly (or something) and rely on mono and/or .NET framework; I do not want them (I only want to support dotnet-core without .NET Framework 3 or 4), so opted to go with source code instead
* SDL2-CS is relying on .NET 4 framework, but I don't need it; hence I do `git submodule` and hand-delete their .csproj files and use my own
* SDL2-CS is just a wrapper, I started doing my own dllImport but honestly, I'm purely lazy, so decided to just take SDL2-CS (just the src dir) instead
* Possibly, will contact the maintainer of SDL2-CS and request a pure dotnetcore CSProj file; either that or fork and just remove the author's csproj
* Sample code from: https://www.willusher.io/sdl2%20tutorials/2013/08/17/lesson-1-hello-world
* Finally, I'm no expert of Functional Programming, but as far as it being from my experiences, grabbing fragments/snippets of codes from F# and making it your own is almost like doing a 500 pieces jigsaw puzzles where they are all white pieces...  Rather than trying to take implementations of pieces, it's better to just try to comprehend its intentions and write your own, it will most likely be quicker!

## Future work
* If (or when) have time, will completely port SDL2-CS to F# rather than having C# be the InterOp to InterOp; as mentioned, I got tired of doing `[<dllImport>]` to extern, and found SDL2-CS doing the same work, so decided not to reinvent the wheel
* I believe libsdl is zlib license, so will have to possibly update the LICENSE file to match

## Build
Tested only on Debian
1. cd SDL2-CSharp
2. git submodule add https://github.com/flibitijibibo/SDL2-CS
3. cd SDL2-CS
4. rm \*csproj
5. cd ..
6. dotnet build

## Run
$ dotnet run --project app/

![Screenshot](Screenshot.png)
