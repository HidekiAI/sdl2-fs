namespace sdlfs

open System

module libVulkan =
    let init = libSDLWrapper.initSDLVulkan

    let getWindow winTitle winXPos winYPos winWidth winHeight =
        libSDLWrapper.getWindowVulkan winTitle winXPos winYPos winWidth winHeight
