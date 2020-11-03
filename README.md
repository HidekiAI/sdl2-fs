# SDL2 on F#

Sample code to interface with https://www.libsdl.org/

## Summary
* dotnet (without the .NET 4 Framework and/or mono)
* F# init call to libSDL2.so via wrapper
* Using SDL2-CS (SDL2 C#) from https://github.com/flibitijibibo/SDL2-CS (see notes)

## Prerequisites
* libSDL2 (please don't ask me about Windows or Mac), I will just assume you're here because you already are using it or have installed it

## Future work
* If (or when) have time, will completely port SDL2-CS to F# rather than having C# be the InterOp to InterOp; as mentioned, I got tired of doing `[<dllImport>]` to extern, and found SDL2-CS doing the same work, so decided not to reinvent the wheel
* I believe libsdl is zlib license, so will have to possibly update the LICENSE file to match

## Notes
* There are handful of NuGet that wraps SDL2; only tried one or two, but they want to be MonoGames or OpenTK friendly (or something) and rely on mono and/or .NET framework; I do not want them, so opted to go with source code instead
* SDL2-CS is relying on .NET 4 framework, but I don't need it; hence I do `git submodule` and hand-delete their .csproj files and use my own
* SDL2-CS is just a wrapper, I started doing my own dllImport but honestly, I'm purely lazy, so decided to just take SDL2-CS (just the src dir) instead
* Possibly, will contact the maintainer of SDL2-CS and request a pure dotnetcore CSProj file; either that or fork and just remove the author's csproj
* Sample code from: https://www.willusher.io/sdl2%20tutorials/2013/08/17/lesson-1-hello-world

### Caveats (coding practice and philosophies)
Firstly, if you're helping/participating, and you're using Windows, please
ignore my rants, and continue to follow your patterns and practices of relying
on type inferrances.  This rant is about those holier-than-thou people who will
preach me about using type inferrances and will just tell me "just use Windows"
as their prejudiced views...

You will see a lot more anti-type inferences in my code; it isn't that
I'm against it; in fact I prefer type inferences, for it makes it cleaner
(easier to read without interruptions of switching thoughts).  
Unfortunately, at the time of this writing, if you are programming on Linux, 
tools such as fsautocomplete either hangs (my theory is that it isn't hanging, 
it's more that the cache gets so huge that it struggles on swap to memory I/O, 
etc) causing LSC (via LSP) almost unusable (both in VSCode and via vim).  
With so-called "intelli-sense" not so accessible (I also dislike the habit of 
mouse-over-hover practice, leaving my hands from home position on my keyboard; 
on Vim, when it works, it either shows on the gutter or small window pane, 
which is much nicer).
With that said, if you cannot rely on static code analysis tools in real-time,
and to avoid wasteful hopping from files to files to figure out the signatures
for currying, I rather explicitly define functions and lambdas with types and
leave it as-is (yes, I could remove it once done, but why?).
All in all, before you criticize my coding patterns and you've only coded on
Windows, as well as you've not yet spent at least 2 hours (or more) analyzing
and strace'ing `fsautocomplete.dll` (i.e. filepath is Windows biased) to a
point where you'd just give up, then do so first, and then maybe we can have a
more civilized discussion on how I can readjust my F# coding patterns...

### TODO (collaborations)
* Entities and world object management, scene (or level) management
* Possibly AI management (or as Valve engine calls "Director")
* Sound management - mainly resource management
* Sprite management - mainly resource management
* Background management - mainly resource management

Optional:
* Possibly better vectors and matrices library that are less generic and more specialized for 2D/3D (just need 4x4) and methods such as scale, rotate/orient (also in quarturnion), and translate
* Vulkan support - probably useful for particle system?
* Physics (collision) system - for now, sprite collision is basically testing for overlap of sprite collision-boundaries, but does not alter velocity/acceleration of the sprites

"Managers" are mainly for the necessary-evil needs for managing (persisting)
Unsafe/unmanaged resources (i.e. SDL Texture, audio file).  Possibly, an 
anternative is to have a lower-level "ResourceManager" but I dislike the patterns 
which system-A becomes coupled with system-B; unit-tests are painful to write 
for higher level stuffs...  Self-containment is much easier to collaborate...

Other "managers" (such as Entity and AI) that does not tie/couple to SDL
directly, are there just for the sake of game-engine type design; but is
extremely lightweight, but needs to be tied to, or interacts with, user
interface directly or indirectly, and so they need to be managed.

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

