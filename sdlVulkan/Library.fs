namespace sdlfs

open System
open SDL2 // we'll be accessing SDL_Texture

module libVulkan =
    let init = libSDLWrapper.initSDLVulkan

    let getWindow winTitle winXPos winYPos winWidth winHeight =
        libSDLWrapper.getWindowVulkan winTitle winXPos winYPos winWidth winHeight
