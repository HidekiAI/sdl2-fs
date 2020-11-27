// Orinal Author: HidekiAI@users.noreply.github.com
// Project: git@github.com:HidekiAI/sdl2-fs
// Caveat: Read my README.Md on why anti-type inferences is practiced here...
namespace sdlfs

open System
open System.Numerics
open SDL2 // we'll be accessing SDL_Texture
open Newtonsoft.Json // deserialization of Sprite file

// TODO: Decide whether we need a flag to indicate that "Up" (in y-axis) is positive or negative
// If "Up" means positive and "Right" is also positive, having an UpperRightCorner of local-coordinate (or relative coordinate)
// is all we need because we can assume (0,0) is always the origin.  In which case, all we need is
// Width (positive X) and Height (positive Y).
// NOTE: width and height should never reach more than 65535 pixels tall or wide.  If you are
//       insane enough to want a texture that is that huge, you're either trying to break the
//       GPU or just being silly...  redesign and either use ScaleX/ScaleY, or break it down
//       into multiple sprites and stitch them
// NOTE2: It's uint16 rather than uint8, since it would be inconvinient to have a texture size
//        that is greater than 255 pixels and needing to stitch them... (i.e. on low framerate,
//        it can cause tears or clipped too early)
// Sprite rects, though not constrained/restricted should be power-of-two for performance reason!
// Breif note about SDL_Rect (more so, about C/C++ struct): Look into [<StructLayout>], [<MarshalAs>], and [<DefaultValue>]
[<System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)>]
type TSpriteRect = SDL.SDL_Rect
//    val x: int32
//    val y: int32
//    val w: int32
//    val h: int32
//    new(_x, _y, _w, _h) =
//        { x = _x; y = _y; w = _w; h = _h }

// Future considerations:
// * for textures bitmap/atlas that could not be loaded (i.e. bigger than 64K pixels) should be assigned with annoyingly bright magenta textures
type TSpriteCell =
    { ID: uint32 // 1-based ID, 0 means ghost sprite (has collision rect of 0-pixel, useful for animation to use blank)
      TextureID: uint32 // 1-based ID, you'll have to use this.to lookup where the origin of texture/atlas file was/is from; if this.is 0, but has CollisionRect, you can use it as an invisible sprite for something like invisible walls
      RelativeBoundary: TSpriteRect // mainly used as dimension of the sprite for clipping, but it is also power of 2 (8, 16, 32, etc)
      RelativeCollisionRect: TSpriteRect // has to be equal and/or less than the Boundary rect
      RelativeHotPoint: Vector4 }

// Sprite Animation:
// Suppose you'd want to animate sprite cell IDs in sequence of {1, 2, 3, Pause, 4, 3, 4, 3, ...}
// at 1 frame per second (1.0 FPS), but we'd want the Pause to take 3 seconds on the last frame
// To avoid having a mutable variable to render last frame SpriteID, pauxe will require SpriteID,
// hence it would be sequenced as:
//      [
//          // Index: 0..2
//          {SpriteID=1,FPS=1.0}; {SpriteID=2,FPS=1.0}; {SpriteID=3,FPS=1.0};
//          // Index: 3
//          {SpriteID=3,FPS=4.0}; // longer FPS gives an effect of pausing...  Note that FPS here is 4 instead of desired 5 seconds, because previous frame was already waiting 1 second
//          // Index: 4..5
//          {SpriteID=4,FPS=1.0}; {SpriteID=3,FPS=1.0};
//          {LoopIndex=4}   // loop back to Index=4
//      ]
// Other example: Supposed you want to render a dot that turns into a ball (i.e. ". o @ ") and once it
// reaches the final frame, you want it to stay on that frame.  It would be [ID=1, ID=2, ID=3, Loop=2]
// one note is it would probably be good to set the final frame's FPS to be reasonably large number so
// that it won't flicker on a platform that runs at low-FPS...
type TSpriteFrame =
    {
      // wish there is unsigned float, we cannot allow negative FPS!
      FPS: float // i.e. 1.5 means it will render for 1.5 seconds before switching to next frame
      SpriteCellID: uint32 } // 1-based SrpiteCell.ID (see CurrentCellIndex)

type TSpriteAnimation =
    | Frame of TSpriteFrame
    | Loop of uint32    // index back to animation FRAME index

type TSpriteCellAnimation =
    { // To get spriteCell/Texture Index, reference animIndex to get SpriteFrame, for Cells are used in multiple/different anim sequences
      CurrentAnimationIndex: int32
      // Tick is in milliseconds, so if a loop takes less than 1.0 mSec (i.e. 999 microSec) then it'll be 0!
      CurrentAnimationTick: float }

// One animation per sprite; there are some sprites that are from same SpriteCells shared for each Animations
type TSprite =
    { ID: uint32 // 1-based ID, Not to be confused from SpriteCell.ID (unlike SpriteCell.ID==0, this.ID can never be 0!  if it is 0, it is just a place-holder of a "null sprite" - usually because Texture cannot be found any more)
      Name: string // in most use-cases, Sprite.ID is meaningless compared to Sprite.Name

      Cells: TSpriteCell []
      // Scale, orientation, and translation
      // Last vector is translation (position) for rendering, for example the hot-point (which is the local coordinate) that gets ADDED to this world cordinate
      SpriteMatrix: Matrix4x4

      // One Animation sequence per Sprite
      Animations: TSpriteAnimation []
      StepDirection: int8 // +1 to loop forward, 0 to not loop at all (if multiple SpriteID[] exist, always at SpriteID[0]), -1 to loop backwards
      SpriteAnimationInfo: TSpriteCellAnimation

      // With Acceleration and Velocity, it can deal with primitive Newtonian physics
      Velocity: Vector4
      Acceleration: Vector4
      // Sprite collision based on overlapping of collision boundary, not sprite boundaries.
      // This array of Sprite.ID's that overlaps/collides with this sprite; meaning
      // if spriteA.Overlapped contains spriteB, on spriteB.Overlapped, it contains spriteA
      // by implementations, one should keep track of `ProcessedSprites: Sprites[]` list
      // so that you do not do redundant checks, and if possible recursively down the tree branches...
      // If/when there is a collision system that starts to manage its own collision tree, perhaps
      // we can remove this element; it is also here to make the sprite self-aware of its neighbors
      // in which, again, if/when entity system (separate from collsion system) takes over so
      // Entity hasA Sprite, this can move over to Entity.Overlapped instead...
      Overlapped: uint32 []
      // persistence
      DeleteWhenOffScreen: bool // when you start giving velocities to a sprite, it has the tendencies to just go and disappear...
      SleepWhenOffScreen: bool // possibly, all these  off-screen stuff can be D.U. based...
      // Sound associated to animation, only relevant when AnimationFreqecy is > 0, Loop != 0, and Sprites.Length > 1
      SoundID: uint32 } // 1-based ID, 0 means no sound

///////////////////////////////////////////////////////////// Sprite File
type TSpriteFileSprite =
    { ID: uint32
      X: uint32
      Y: uint32
      Width: uint16
      Height: uint16
      HotPointX: uint32
      HotPointY: uint32
      CollisionX: uint32
      CollsionY: uint32
      CollsionWidth: uint16
      CollisionHeight: uint16 }

type TSpriteFileAnimationAnimationFrame = { ID: uint32; FPS: float }

type TSpriteFileAnimationAnimation =
    { Frames: TSpriteFileAnimationAnimationFrame []
      StepDirection: int8 // can go positive/negative
      RepeatLoopIndex: uint32 }

type TSpriteFileAnimation =
    { Name: string
      Animation: TSpriteFileAnimationAnimation }

type TSpriteFile =
    { Image: string
      Sprites: TSpriteFileSprite []
      Animations: TSpriteFileAnimation [] }

///////////////////////////////////////////////////////////// Sprite File
type SpriteBuilder(maxSprites) =
    // ID's do not need to be static, it can stay internal to each sprite systems
    // but any sprites added to each (this.X) sprite system should not collide/overlap
    // on IDs
    let mutable spriteCellID: uint32 = 0u
    let mutable spriteID: uint32 = 0u // Q: Do we need this?  Users will most likely manage based on Sprite.Name
    // disposable list...
    let mutable textures: IntPtr [] = Array.zeroCreate maxSprites
    let mutable textureIndex: int32 = 0

    let mapTextureIndexToID =
        textures
        |> Array.mapi (fun index pTexture -> {| Index = index; Texture = pTexture |})

    let getWorkDirectory file =
        if System.IO.File.Exists file then
            let fileInfo =
                System.IO.FileInfo file
            fileInfo.Directory.FullName + "/"
        else
            SDL.SDL_LogError(int(SDL.SDL_LogCategory.SDL_LOG_CATEGORY_APPLICATION), sprintf "File does not exsit, cannot extract directory path for '%A'" file)
            // return path as local (relative) path
            "./"

    let loadAnimation file =
        let json =
            System.IO.File.ReadAllLines(file)
            |> fun s -> String.Join(String.Empty, s)

        JsonConvert.DeserializeObject<TSpriteFile>(json)

    let buildSpriteCell (animSprite: TSpriteFileSprite) textureIndexId =
        { ID = animSprite.ID
          TextureID = textureIndexId
          RelativeBoundary = TSpriteRect(x = 0, y = 0, w = int32 (animSprite.Width), h = int32 (animSprite.Height))
          RelativeCollisionRect =
              TSpriteRect(x = 0, y = 0, w = int32 (animSprite.CollsionWidth), h = int32 (animSprite.CollisionHeight))
          RelativeHotPoint = Vector4(float32 (animSprite.HotPointX), float32 (animSprite.HotPointY), 0.0f, 0.0f) }

    let buildSprite spriteId (spriteFileSprite: TSpriteFileAnimation) (spriteCells: TSpriteCell []) animations =
        { ID = spriteId
          Name = spriteFileSprite.Name
          Cells = spriteCells
          Animations = animations
          StepDirection = spriteFileSprite.Animation.StepDirection
          SpriteAnimationInfo =
              {
                CurrentAnimationIndex = 0
                CurrentAnimationTick = 0.0 }
          Velocity = Vector4(0.0f, 0.0f, 0.0f, 0.0f)
          Acceleration = Vector4(0.0f, 0.0f, 0.0f, 0.0f)
          Overlapped = [||]
          DeleteWhenOffScreen = false
          SleepWhenOffScreen = true
          SoundID = 0u
          SpriteMatrix = Matrix4x4.Identity }

    // base CellId should not get incremented until ALL cell from the file is processed
    // this is because the ID in the json file is relative to that file, so if last file loaded
    // had updated baseCellID to be 5, and there are ID={0..4}, then from array point of view,
    // they are actually CellID={5..9}
    let buildAnimFrame baseSpriteCellId (frame: TSpriteFileAnimationAnimationFrame option) (loop: uint32 option) =
        match frame, loop with
        | Some f, None ->
            let ff =
                { FPS = f.FPS
                  SpriteCellID = f.ID + baseSpriteCellId }

            TSpriteAnimation.Frame(ff)
        | None, Some l -> TSpriteAnimation.Loop(l)
        | None, None -> failwith "Either frame or loop data has to be valid"
        | Some _, Some _ -> failwith "Cannot have both looping and frame info to be populated"

    /// load to GPU as texture
    /// TODO: In future, support software renderer by tracking surface (currently only texture for GPU)
    let load window renderer jsonFileName (spriteAnimationJson: TSpriteFile): TSprite [] =
        let workDir =
            getWorkDirectory jsonFileName
        let imageName =
            workDir + spriteAnimationJson.Image
        SDL.SDL_LogDebug(int(SDL.SDL_LogCategory.SDL_LOG_CATEGORY_APPLICATION), sprintf "Attempting to load texture image '%A'" imageName)
        // blit to RAM
        let surface =
            libSDLWrapper.getSurfaceBitMap window renderer false imageName // assum this to throw, so it'll bail out if this fails to load

        // now that we know it's safe to RAM, move it over to GPU (as texture)
        // persist texture (because they need to be disposed on shutdown) and incremnt (global) textureIndex
        let destroySurface = true

        let texture =
            if not (surface.Equals(IntPtr.Zero))
            then libSDLWrapper.getTexture window renderer surface destroySurface false
            else failwith "Unable to load Sprite atlas file"

        if textureIndex >= textures.Length
        then failwith "Texture index exceeds texture (initialized) capacity"

        textures.[textureIndex] <- texture
        textureIndex <- textureIndex + 1

        let spriteCells =
            spriteAnimationJson.Sprites
            |> Array.mapi (fun spriteIndex sprite -> buildSpriteCell sprite (uint32 (textureIndex - 1))) // use the TextureID of last loaded texture

        let buildAnimation spriteFileAnimationAnaimation: TSpriteAnimation [] =
            spriteFileAnimationAnaimation.Frames
            |> Array.map (fun frame -> buildAnimFrame spriteCellID (Some frame) None)
            |> fun a ->
                let loop =
                    buildAnimFrame spriteCellID None (Some spriteFileAnimationAnaimation.RepeatLoopIndex)

                // make sure looping frame is at the tail of the animation sequences
                Array.concat [ a; [| loop |] ]

        // Each Sprite contains one sequences of SpriteAnimation
        let buildAnimationSprites (sprites: TSpriteFileSprite []) (animations: TSpriteFileAnimation []) =
            animations
            |> Array.mapi (fun spriteIndex animSprite ->
                buildAnimation animSprite.Animation
                |> buildSprite (uint32 (spriteID + uint32 (spriteIndex))) animSprite spriteCells)

        let sprites =
            buildAnimationSprites spriteAnimationJson.Sprites spriteAnimationJson.Animations

        spriteID <- spriteID + uint32 (sprites.Length)
        SDL.SDL_LogDebug(int(SDL.SDL_LogCategory.SDL_LOG_CATEGORY_APPLICATION), (sprintf "%A" sprites))
        sprites // TODO: Possibly persist this and expose `this.GetSprites` member?  IMHO if the caller loses scope of this array, it's their fault!

    let getWorldPosition sprite =
        let transX = sprite.SpriteMatrix.M41
        let transY = sprite.SpriteMatrix.M42
        let transZ = sprite.SpriteMatrix.M43
        Vector4(transX, transY, transZ, 0.0f)

    let rec getSpriteFrame (kAnimations: TSpriteAnimation[]) spriteAnimIndex : TSpriteFrame =
        match kAnimations.[spriteAnimIndex] with
        | Frame f -> f
        | Loop l -> getSpriteFrame kAnimations (int32 l)
    let updateAnimation sprite deltaTicks: TSprite =
        let info = sprite.SpriteAnimationInfo
        let mutable nextTick = info.CurrentAnimationTick + deltaTicks
        let nextAnimFrameIndex =
            match sprite.Animations.[info.CurrentAnimationIndex] with
            | Frame f ->
                if (f.FPS * 1000.0) > nextTick then
                    nextTick <- (f.FPS * 1000.0) - nextTick // rather than resetting to 0, set it to tick where it should be to make animation smoother
                    if info.CurrentAnimationIndex + 1 > sprite.Animations.Length then
                        info.CurrentAnimationIndex
                    else
                        info.CurrentAnimationIndex + 1
                else
                    info.CurrentAnimationIndex
            | Loop li -> int32(li)
        let newInfo = { info with CurrentAnimationIndex = nextAnimFrameIndex; CurrentAnimationTick = nextTick; }
        { sprite with SpriteAnimationInfo = newInfo; }

    ///////////////////////////////////////////////////////// public interfaces
    /// MaxSpriteIndex is useful/needed for iterating Sprites as Entity
    member this.MaxSpriteIndex = spriteID

    /// Perhaps this should be called CleanUp or Destroy?
    member this.Dispose =
        for tIndex in 0 .. textureIndex do
            SDL.SDL_DestroyTexture(textures.[tIndex])
            textures.[tIndex] <- IntPtr.Zero

        textureIndex <- 0

    /// Tools (i.e. world editor) and world systems just calls this and will get list of Sprite[]
    member this.Load window renderer jsonFilename =
        loadAnimation jsonFilename
        |> load window renderer jsonFilename

    member this.Draw (renderer: IntPtr) window deltaTicks (sprites: TSprite[]): TSprite[] =
        printfn "Draw begin (%A sprites)" sprites.Length
        let textureMap = mapTextureIndexToID

        let mutable winWidth = 1024
        let mutable winHeight = 768
        SDL.SDL_GL_GetDrawableSize(window, ref winWidth, ref winHeight) // either GL or SDL_Vulkan_GetDrawableSize

        let windowRect =
            TSpriteRect(x = 0, y = 0, w = int32 (winWidth), h = int32 (winHeight))

        let updatedSprites =
            sprites
            |> Array.mapi (fun spriteIndex animSprite ->
                let info = animSprite.SpriteAnimationInfo
                let spriteFrame =
                    getSpriteFrame animSprite.Animations info.CurrentAnimationIndex
                let textureIndex =
                    textureMap
                    |> Array.find (fun elem -> uint32 (elem.Index) = spriteFrame.SpriteCellID)
                    |> fun il -> il.Index
                let cell = animSprite.Cells.[textureIndex]
                let spriteRect = cell.RelativeCollisionRect
                let texture = textures.[int32(cell.TextureID)]
                printfn "%A) Rendering '%A' #%A at (%A, %A, %A, %A) - Win: (%A, %A, %A, %A)" spriteIndex animSprite.Name textureIndex spriteRect.x spriteRect.y spriteRect.w spriteRect.h windowRect.x windowRect.y windowRect.w windowRect.h
                let success =
                    SDL.SDL_RenderCopy(renderer, texture, ref spriteRect, ref windowRect)
                if success <> 0 then
                    SDL.SDL_LogError(int(SDL.SDL_LogCategory.SDL_LOG_CATEGORY_RENDER), sprintf "Unable to render sprite #%A (texture #%A)" spriteIndex textureIndex)
                updateAnimation animSprite deltaTicks
            )
        printfn "Draw end (%A)" updatedSprites.Length
        updatedSprites

    member this.GetWorldPostionHotPoint sprite =
        let pos = getWorldPosition sprite
        let currentAnimInfo = sprite.SpriteAnimationInfo
        let animFrame =
            getSpriteFrame sprite.Animations currentAnimInfo.CurrentAnimationIndex

        let hotPointX =
            sprite.Cells.[int32(animFrame.SpriteCellID)]
                .RelativeHotPoint.X

        let hotPointY =
            sprite.Cells.[int32(animFrame.SpriteCellID)]
                .RelativeHotPoint.Y

        let hotPointZ =
            sprite.Cells.[int32(animFrame.SpriteCellID)]
                .RelativeHotPoint.Z
        // NOTE: Possibly just use the darn MathLib's vector addition
        Vector4(pos.X + hotPointX, pos.Y + hotPointY, pos.Z + hotPointZ, 0.0f)

    member this.WorldTranslateLocal sprite (localPositionVector: Vector4) =
        let pos = getWorldPosition sprite

        let newPos =
            Vector4(pos.X + localPositionVector.X, pos.Y + localPositionVector.Y, pos.Z + localPositionVector.Z, 0.0f)

        let m = sprite.SpriteMatrix

        let newMatrix =
            Matrix4x4
                (m.M11, m.M12, m.M13, m.M14,
                 m.M21, m.M22, m.M23, m.M24,
                 m.M31, m.M32, m.M33, m.M34,
                 newPos.X, newPos.Y, newPos.Z, m.M44)

        let newSprite = { sprite with SpriteMatrix = newMatrix }
        newSprite

    member this.SetWorldTranslate sprite (absoluteWorldPositionVector: Vector4) =
        let m = sprite.SpriteMatrix

        let newMatrix =
            Matrix4x4
                (m.M11, m.M12, m.M13, m.M14,
                 m.M21, m.M22, m.M23, m.M24,
                 m.M31, m.M32, m.M33, m.M34,
                 absoluteWorldPositionVector.X, absoluteWorldPositionVector.Y, absoluteWorldPositionVector.Z, m.M44)

        let newSprite = { sprite with SpriteMatrix = newMatrix }
        newSprite
