namespace libSDL2Wrapper
open System
open SDL2

module HelloWorld =
    let initSDL =
        match SDL.SDL_Init(SDL.SDL_INIT_VIDEO) with
            | 0 -> 0
            | v -> 
                let strErr = SDL.SDL_GetError()
                let err = 
                    sprintf "SDL_Init() - Unable to initialze SDL: %s" strErr
                SDL.SDL_Log(err)
                SDL.SDL_Quit()
                failwith err

    let getWindow winTitle winXPos winYPos winWidth winHeight = 
        let pInterOp = SDL.SDL_CreateWindow(winTitle, winXPos, winYPos, winWidth, winHeight, SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN)
        if pInterOp.Equals(IntPtr.Zero) then
            let strErr = SDL.SDL_GetError()
            let err = 
                sprintf "SDL_CreateWindow() - Unable to create Window: %s" strErr
            SDL.SDL_Log(err)
            SDL.SDL_Quit()
            failwith err
        else
            pInterOp   

    let getRenderer window = 
        let pInterOp = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED ||| SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC)
        if pInterOp.Equals(IntPtr.Zero) then
            let strErr = SDL.SDL_GetError()
            let err = 
                sprintf "SDL_CreateRenderer() - Unable to create Renderer: %s" strErr
            SDL.SDL_Log(err)
            SDL.SDL_Quit()
            failwith err
        else
            pInterOp   

    let getSurfaceBitMap window renderer imagePath =
        let pInterOp = SDL.SDL_LoadBMP(imagePath)
        if pInterOp.Equals(IntPtr.Zero) then
            let strErr = SDL.SDL_GetError()
            SDL.SDL_DestroyRenderer(renderer)
            SDL.SDL_DestroyWindow(window)
            let err = 
                sprintf "SDL_LoadBMP() - Unable to load bitmap '%s': %s" imagePath strErr
            SDL.SDL_Log(err)
            SDL.SDL_Quit()
            failwith err
        else
            pInterOp   

    let getTexture window renderer surfaceBitMap = 
        let pInterOp = SDL.SDL_CreateTextureFromSurface(renderer, surfaceBitMap)
        if pInterOp.Equals(IntPtr.Zero) then
            let strErr = SDL.SDL_GetError()
            SDL.SDL_DestroyRenderer(renderer)
            SDL.SDL_DestroyWindow(window)
            let err = 
                sprintf "SDL_CreateTextureFromSurface() - Unable to create texture suface: %s" strErr
            SDL.SDL_Log(err)
            SDL.SDL_Quit()
            failwith err
        else
            pInterOp   

    let doTestRender renderer texture (syncMS: uint32) =
        for i in [0..5] do  // 1000mSec x 5 = 5 seconds
            SDL.SDL_RenderClear(renderer)
            |> ignore
            SDL.SDL_RenderCopy(renderer, texture, IntPtr.Zero, IntPtr.Zero)
            |> ignore
            SDL.SDL_RenderPresent(renderer)
            SDL.SDL_Delay(syncMS)

    let shutdown window renderer texture =
        SDL.SDL_DestroyTexture(texture)
        SDL.SDL_DestroyRenderer(renderer)
        SDL.SDL_DestroyWindow(window)
        SDL.SDL_Quit()
