open System
open System.Numerics
open sdlfs
open SDL2

module MEvents =
    type TMouseEvent =
        | Button of SDL.SDL_MouseButtonEvent
        | Motion of SDL.SDL_MouseMotionEvent
        | Wheel of SDL.SDL_MouseWheelEvent

[<EntryPoint>]
let main argv =
    let bgImageName = @"sdllogo.bmp" // TODO: Pass this via arg

    let absPathBg =
        System.IO.Directory.GetCurrentDirectory()
        + @"/"
        + bgImageName

    match libSDLWrapper.initSDLVideo with
    | 0 ->
        // setup Window and Renderer
        let winPosX = 100
        let winPosY = 100
        let winWidth = ref 1024
        let winHeight = ref 768
        let winTitle = @"F# SDL2 Test"
        let window:IntPtr =
            libSDLWrapper.getWindow winTitle winPosX winPosY winWidth.Value winHeight.Value
        let renderer:IntPtr = libSDLWrapper.getRenderer window
        // sanity check to make sure we got the right window and renderer
        winWidth := 0
        winHeight := 0
        SDL.SDL_GetWindowSize(window, winWidth, winHeight) // either GL or SDL_Vulkan_GetDrawableSize
        SDL.SDL_LogInfo(int(SDL.SDL_LogCategory.SDL_LOG_CATEGORY_APPLICATION), sprintf "Created window size (%A x %A)" winWidth.Value winHeight.Value)
        winWidth := 0
        winHeight := 0
        SDL.SDL_GetRendererOutputSize(renderer, winWidth, winHeight)
        |> ignore
        SDL.SDL_LogInfo(int(SDL.SDL_LogCategory.SDL_LOG_CATEGORY_APPLICATION), sprintf "Created renderer size (%A x %A)" winWidth.Value winHeight.Value)

        // load background image
        let bgSurfaceBitMap =
            libSDLWrapper.getSurfaceBitMap window renderer true absPathBg
        let bgTexture =
            libSDLWrapper.getTexture window renderer bgSurfaceBitMap true true
        libSDLWrapper.doRender renderer bgTexture
        |> ignore

        // Now load test animation sprites and build it
        let spriteBuilder = sdlfs.SpriteBuilder(32)
        let mutable sprites =
            spriteBuilder.Load window renderer @"data/Sonic-Idle.json"
        // move two sprites into different locations, one is idle, one is impatient
        let posIdle = Vector3(512f, 127f, 0.0f)
        sprites.[0] <- spriteBuilder.SetWorldTranslateLocal sprites.[0] posIdle
        let posImpatient = Vector3(512f, 512f, 0.0f)
        sprites.[1] <- spriteBuilder.SetWorldTranslateLocal sprites.[1] posImpatient
        sprites.[1] <- spriteBuilder.XFlip sprites.[1]
        sprites.[1] <- spriteBuilder.AddWorldAccel sprites.[1] (Vector3(0.5f, 0.5f, 0.0f))

        // Event loop
        let handleWindowEvents (wEvent: SDL.SDL_WindowEvent) =
            match wEvent.windowEvent with
            | event when SDL.SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED = event ->
                let msg =
                    sprintf "Window resized: %Ax%A" wEvent.data1 wEvent.data2
                SDL.SDL_LogDebug(int(SDL.SDL_LogCategory.SDL_LOG_CATEGORY_APPLICATION), msg)
            | event when SDL.SDL_WindowEventID.SDL_WINDOWEVENT_SIZE_CHANGED = event ->
                let msg = sprintf "Window size changed: %Ax%A" wEvent.data1 wEvent.data2  // possibly data1&2 not available and only on RESIZED
                SDL.SDL_LogDebug(int(SDL.SDL_LogCategory.SDL_LOG_CATEGORY_APPLICATION), msg)
            | event when SDL.SDL_WindowEventID.SDL_WINDOWEVENT_CLOSE = event ->
                let msg = sprintf "Window closed: %Ax%A" wEvent.data1 wEvent.data2
                SDL.SDL_LogDebug(int(SDL.SDL_LogCategory.SDL_LOG_CATEGORY_APPLICATION), msg)
                let mutable refEvent = SDL.SDL_Event()
                refEvent.typeFSharp <- SDL.SDL_EventType.SDL_QUIT
                SDL.SDL_PushEvent(ref refEvent) |> ignore
            | _ -> failwith "Unhandled Window Event"

        SDL.SDL_LogDebug(int(SDL.SDL_LogCategory.SDL_LOG_CATEGORY_APPLICATION), "Begin event polling...")
        //let handleMouseEvents (mEvent: MEvents.TMouseEvent) = ;
        let mutable sdlEvent: SDL.SDL_Event = SDL.SDL_Event()
        let mutable quit = false
        let mutable deltaTMSec = 0.0
#if DEBUG
        let mutable tryCount = 0
#endif
        while (not quit) do
            let startTime =
                System.Diagnostics.Stopwatch.StartNew()
            if SDL.SDL_RenderClear(renderer) <> 0 then
                let strErr = SDL.SDL_GetError()
                let err =
                    sprintf "SDL_RenderClear() - Unable to SDL_RenderClear: %s" strErr
                SDL.SDL_LogCritical(int(SDL.SDL_LogCategory.SDL_LOG_CATEGORY_RENDER), err)
            // first procecess any O/S events
            SDL.SDL_PumpEvents()
            while ((SDL.SDL_PollEvent(ref sdlEvent) = 1) && (not quit)) do
                if  (sdlEvent.typeFSharp <> SDL.SDL_EventType.SDL_FIRSTEVENT) then
                    printfn "%A (%A)" sdlEvent.typeFSharp sdlEvent.window.windowEvent
                    match sdlEvent.typeFSharp with
                    | event when SDL.SDL_EventType.SDL_WINDOWEVENT = event ->
                        handleWindowEvents sdlEvent.window
                    | event when SDL.SDL_EventType.SDL_KEYDOWN = event ->
                        let scanCode = sdlEvent.key.keysym.scancode
                        printfn "%A" scanCode
                        if scanCode = SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE then
                            let mutable refEvent = SDL.SDL_Event()
                            refEvent.typeFSharp <- SDL.SDL_EventType.SDL_QUIT
                            SDL.SDL_PushEvent(ref refEvent) |> ignore
                    | event when SDL.SDL_EventType.SDL_QUIT = event ->
                        quit <- true
                        SDL.SDL_LogInfo(int(SDL.SDL_LogCategory.SDL_LOG_CATEGORY_APPLICATION), "QUIT event detected; Shutting down application...")
                        SDL.SDL_Quit()
                    | _ -> // do nothing
                        System.Threading.Thread.Sleep 0 // inner loop just yields
                        |> ignore
            // Now do other gameloop stuffs
            if not quit then
                // First, present BG
                if libSDLWrapper.doRender renderer bgTexture <> 0 then
                    failwith "Unable to render background"
                // Next, animate
                let newSprites =
                    spriteBuilder.DrawAndUpdate window renderer deltaTMSec sprites
                sprites <- newSprites
            // each game ticks are 1mSec
            SDL.SDL_Delay(1u)
            System.Threading.Thread.Sleep 1 // outer loop waits 1 mSec
            |> ignore
            // finally, present the renderer
            SDL.SDL_RenderPresent(renderer)
            deltaTMSec <- float(startTime.ElapsedMilliseconds) + 1.0    // although the Sleep above will make sure it's 1mSec or more, we'll add 1mSec extra (harmless for a system based on FPS)
#if DEBUG
            // for now, die after 30 seconds
            tryCount <- tryCount + 1
            if tryCount > (1000 * 30) then
                quit <- true
#endif

        SDL.SDL_LogDebug(int(SDL.SDL_LogCategory.SDL_LOG_CATEGORY_APPLICATION), "Done event polling...")
        //libSDLWrapper.shutdown window renderer texture
        0 // return 0 as success

    | errVal -> errVal
