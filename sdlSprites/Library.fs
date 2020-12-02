// Orinal Author: HidekiAI@users.noreply.github.com
// Project: git@github.com:HidekiAI/sdl2-fs
// Caveat: Read my README.Md on why anti-type inferences is practiced here...
namespace sdlfs

open System
open System.Numerics    // for Matrix4x4 - alternatively, look into system.windows.media.media3d
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
      AbsoluteCellBoundary: TSpriteRect // mainly for rendering, relative to strip/set of Cells
      RelativeCollisionRect: TSpriteRect // relative (to AbsoluteCellBoundary) dimension of the cell for clipping, collision, etc
      RelativeHotPoint: Vector3 }

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
      Velocity: Vector3
      Acceleration: Vector3
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
      SoundID: uint32  // 1-based ID, 0 means no sound
      // custom function per sprite for collision, edge detection, etc
      Physics: TSprite (*this*) -> TSpriteRect(*other*) -> uint32 (*winWidth*) -> uint32 (*winHeight*) -> TSprite(*newThis*)
    }

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
          AbsoluteCellBoundary =
              TSpriteRect(x = int32(animSprite.X), y = int32(animSprite.Y), w = int32 (animSprite.Width), h = int32 (animSprite.Height))
          RelativeCollisionRect =
              TSpriteRect(x = 0, y = 0, w = int32 (animSprite.CollsionWidth), h = int32 (animSprite.CollisionHeight))
          RelativeHotPoint = Vector3(float32(animSprite.HotPointX), float32(animSprite.HotPointY), 0.0f) }

    let rec getSpriteFrame (kAnimations: TSpriteAnimation[]) spriteAnimIndex : TSpriteFrame =
        match kAnimations.[spriteAnimIndex] with
        | Frame f -> f
        | Loop l -> getSpriteFrame kAnimations (int32 l)

    let getCellAndTexturePtr sprite =
        let info = sprite.SpriteAnimationInfo
        let spriteFrame =
            getSpriteFrame sprite.Animations info.CurrentAnimationIndex
        let textureIndex =
            mapTextureIndexToID
            |> Array.find (fun elem -> uint32 (elem.Index) = spriteFrame.SpriteCellID)
            |> fun il -> il.Index
        let cell = sprite.Cells.[textureIndex]
        {| Cell = cell; TexturePtr = textures.[int32(cell.TextureID)] |}

    let setTranslation (matrix: Matrix4x4) (vec3: Vector3): Matrix4x4 =
        Matrix4x4(
            matrix.M11, matrix.M12, matrix.M13, matrix.M14,
            matrix.M21, matrix.M22, matrix.M23, matrix.M24,
            matrix.M31, matrix.M32, matrix.M33, matrix.M34,
            vec3.X, vec3.Y, vec3.Z, 1.0f)

    // WorldPosition returned here is without local relative position
    let getWorldPosition sprite =
        sprite.SpriteMatrix.Translation

    let getLocalHotPoint sprite =
        let cell = (getCellAndTexturePtr sprite).Cell
        cell.RelativeHotPoint

    // WorldPosition returned here is with local relative position added
    let getWorldPositionRelative sprite =
        let localPos = getLocalHotPoint sprite
        let worldPos = getWorldPosition sprite
        Vector3.Add(worldPos, localPos)

    let defaultSpritePhysics (sprite: TSprite) (otherSprite: TSpriteRect) (winWidth: uint32) (winHeight: uint32): TSprite =
        //printfn "\t\tBegin physics - %A (%A)" sprite.Name sprite.SpriteMatrix.Translation   // DELETE ME
        let revSign v =
            if v < 0.0f then
                Math.Abs(v)
            else
                Math.Abs(v) * -1.0f
        let mutable pos = getWorldPositionRelative sprite
        let mutable newV = sprite.Velocity
        let mutable newA = sprite.Acceleration
        if pos.X > float32(winWidth) || pos.X < 0.0f then
            if pos.X > float32(winWidth) then
                pos.X <- float32(winWidth)
            else
                pos.X <- 0.0f
            // bounce
            newV <- Vector3(0.0f, newV.Y, newV.Z)
            newA <- Vector3(revSign newA.X, newA.Y, newA.Z)
            //printfn "\t\t\tSTOP!!!!!"   // DELETE ME
        if pos.Y > float32(winHeight) || pos.Y < 0.0f then
            if pos.Y > float32(winHeight) then
                pos.Y <- float32(winHeight)
            else
                pos.Y <- 0.0f
            // bounce
            newV <- Vector3(newV.X, 0.0f, newV.Z)
            newA <- Vector3(newA.X, revSign newA.Y, newA.Z)
        let localPos = getLocalHotPoint sprite
        let newMatrix = setTranslation sprite.SpriteMatrix (Vector3.Subtract(pos, localPos))
        //printfn "\t\tEnd physics - %A" newMatrix.Translation    // DELETE ME
        {sprite with SpriteMatrix = newMatrix}

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
          Velocity = Vector3.Zero
          Acceleration = Vector3.Zero
          Overlapped = [||]
          DeleteWhenOffScreen = false
          SleepWhenOffScreen = true
          SoundID = 0u
          SpriteMatrix = Matrix4x4.Identity
          Physics = defaultSpritePhysics}

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

    let updateAnimation sprite deltaTicks: TSprite =
        let seconds = deltaTicks / 1000.0
        let info = sprite.SpriteAnimationInfo
        let mutable nextTick = info.CurrentAnimationTick + deltaTicks
        let nextAnimFrameIndex =
            match sprite.Animations.[info.CurrentAnimationIndex] with
            | Frame f ->
                if (f.FPS * 1000.0) <= nextTick then
                    nextTick <- (f.FPS * 1000.0) - nextTick // rather than resetting to 0, set it to tick where it should be to make animation smoother
                    if info.CurrentAnimationIndex + 1 > sprite.Animations.Length then
                        info.CurrentAnimationIndex
                    else
                        info.CurrentAnimationIndex + 1
                else
                    info.CurrentAnimationIndex
            | Loop li -> int32(li)
        let newInfo = { info with CurrentAnimationIndex = nextAnimFrameIndex; CurrentAnimationTick = nextTick; }
        // calculate new velocity based on Acceleration and ticks
        let acceleration =
            Vector3.Multiply(sprite.Acceleration, float32(seconds))
        let newVelocity =
            Vector3.Add(sprite.Velocity, acceleration)
        let translateWorld =
            Vector3.Add((getWorldPosition sprite), newVelocity)
        let newMatrix =
            setTranslation sprite.SpriteMatrix translateWorld

        //printfn "\t%A) %A (%A sec) @ v=%A a=%A - Index=%A/%A (tick=%A)"
        //    sprite.Name translateWorld seconds acceleration newVelocity nextAnimFrameIndex sprite.Animations.Length nextTick // DELETE ME

        { sprite with SpriteAnimationInfo = newInfo; Velocity = newVelocity; SpriteMatrix = newMatrix }

    // from Wikipedia:
    // struct EulerAngles {
    //     double roll, pitch, yaw;
    // };
    //
    // EulerAngles ToEulerAngles(Quaternion q) {
    //     EulerAngles angles;
    //
    //     // roll (x-axis rotation)
    //     double sinrCosp = 2 * (q.w * q.x + q.y * q.z);
    //     double cosrCosp = 1 - 2 * (q.x * q.x + q.y * q.y);
    //     angles.roll = std::atan2(sinrCosp, cosrCosp);
    //
    //     // pitch (y-axis rotation)
    //     double sinp = 2 * (q.w * q.y - q.z * q.x);
    //     if (std::abs(sinp) >= 1)
    //         angles.pitch = std::copysign(M_PI / 2, sinp); // use 90 degrees if out of range
    //     else
    //         angles.pitch = std::asin(sinp);
    //
    //     // yaw (z-axis rotation)
    //     double cosyCosp = 2 * (q.w * q.z + q.x * q.y);
    //     double cosyCosp = 1 - 2 * (q.y * q.y + q.z * q.z);
    //     angles.yaw = std::atan2(cosyCosp, cosyCosp);
    //
    //     return angles;
    // }
    let toEulerAngles (quaternion: Quaternion) =
        // roll (x-axis rotation)
        let sinrCosp = 2.0f * (quaternion.W * quaternion.X + quaternion.Y * quaternion.Z)
        let cosrCosp = 1.0f - 2.0f * (quaternion.X * quaternion.X + quaternion.Y * quaternion.Y)
        let roll = Math.Atan2(float(sinrCosp), float(cosrCosp))

        // pitch (y-axis rotation)
        let sinp = 2.0f * (quaternion.W * quaternion.Y - quaternion.Z * quaternion.X)
        let pitch =
            if (Math.Abs(sinp) >= 1.0f) then
                let sign =
                    if sinp < 0.0f then
                        -1.0
                    else
                        1.0
                (Math.PI / 2.0) * sign // use 90 degrees if out of range
            else
                Math.Asin(float(sinp))

        // yaw (z-axis rotation)
        let cosyCosp = 2.0f * (quaternion.W * quaternion.Z + quaternion.X * quaternion.Y)
        let cosyCosp = 1.0f - 2.0f * (quaternion.Y * quaternion.Y + quaternion.Z * quaternion.Z);
        let yaw = Math.Atan2(float(cosyCosp), float(cosyCosp))
        {| Yaw = yaw; Pitch = pitch; Roll = roll |}

    let getRadian orientationMatrix =
        let mutable scale = Vector3.One
        let mutable rotation = Quaternion.Identity
        let mutable translation = Vector3.Zero
        // NOTE: Numeric.Quaternion references Y-Up, Z-forward (right hand rule)
        if Matrix4x4.Decompose(orientationMatrix, ref scale, ref rotation, ref translation) then
            let angles = toEulerAngles rotation
            angles.Yaw
        else
            0.0

    let convertDegreesToRadian degrees =
        System.Math.PI * degrees / 180.0

    // sort of like lerping rotate, but only returns the orientation matrix
    let reOrient (orientationMatrix: Matrix4x4) deltaRadian: Matrix4x4 =
        let rotZ = Matrix4x4.CreateRotationZ(deltaRadian)
        Matrix4x4.Lerp(orientationMatrix, rotZ, 1.0f)

    // fortunately, for 2D sprite, we only have to worry about orientations in
    // X-Y plane (yaw, Rotate about Z-axis) nor do we have to worry about gimble-lock (CreateFromYawPitchRoll())
    // if you want to get absolute destination, set lerp percentage to 1.0
    // but for smooth rotation animation, try setting lerp to about 25%, and call it
    // 4 times to get to destination orientation
    let rotate sprite radian (lerpWeight: float32) =
        let rotZ = Matrix4x4.CreateRotationZ(radian)
        let m = sprite.SpriteMatrix
        let lerp = Matrix4x4.Lerp(m, rotZ, lerpWeight)
        {sprite with SpriteMatrix = lerp}
    let rotateAbs sprite radian =
        rotate sprite radian 1.0f    // lerp at 100%

    // Scaling is a multiplier; if current scale is set to 2.0, and you pass another 2.0,
    // then the new scale will become 4.0.  Use scaleAbsolute instead if you want
    // to (for example) reset it back to 1.0 scale
    let scale sprite xScale yScale zScale =
        let mutable m = sprite.SpriteMatrix
        m.M11 <- m.M11 * xScale
        m.M22 <- m.M22 * yScale
        m.M33 <- m.M33 * zScale
        {sprite with SpriteMatrix = m}
    let scaleAbsolute sprite xScale yScale zScale =
        let mutable m = sprite.SpriteMatrix
        m.M11 <- xScale
        m.M22 <- yScale
        m.M33 <- zScale
        {sprite with SpriteMatrix = m}

    let setVelocity sprite newVelocity =
        {sprite with Velocity = newVelocity}

    let setAcceleration sprite newAccel =
        {sprite with Acceleration = newAccel}

    ///////////////////////////////////////////////////////// public interfaces
    /// MaxSpriteIndex is useful/needed for iterating Sprites as Entity
    member this.MaxSpriteIndex = spriteID

    /// Dispose/cleanup non-GC resources
    member this.Dispose =
        for tIndex in 0 .. textureIndex do
            SDL.SDL_DestroyTexture(textures.[tIndex])
            textures.[tIndex] <- IntPtr.Zero

        textureIndex <- 0

    /// Load sprite definitions JSON file
    member this.Load window renderer jsonFilename =
        loadAnimation jsonFilename
        |> load window renderer jsonFilename

    /// Render sprites to window and update (animate) with new sprite list
    /// Will update animation prior to rendering sprite cells
    member this.DrawAndUpdate window (renderer: IntPtr) deltaTicks (sprites: TSprite[]): TSprite[] =
        let winWidth = ref 0
        let winHeight = ref 0
        if SDL.SDL_GetRendererOutputSize(renderer, winWidth, winHeight) <> 0 then
            SDL.SDL_LogCritical(int(SDL.SDL_LogCategory.SDL_LOG_CATEGORY_RENDER), sprintf "SDL_GetDesktopDisplayMode failed: %s" (SDL.SDL_GetError()))
            failwith "Unable to retrieve Renderer dimension"
        //printfn "Draw begin (%A sprites) - Dim: %A x %A" sprites.Length winWidth.Value winHeight.Value   // DELETE ME

        let updatedSprites =
            sprites
            |> Array.mapi (fun spriteIndex animSprite ->
                //printfn "Begin %A at %A" animSprite.Name (getWorldPositionRelative animSprite)  // DELETE ME
                // animate and translate sprite
                let mutable updatedSprite =
                    updateAnimation animSprite deltaTicks
                // once moved, do Physics test immediately
                updatedSprite <-
                    updatedSprite.Physics updatedSprite (TSpriteRect(x=0, y=0, w=0, h=0)) (uint32 winWidth.Value) (uint32 winHeight.Value)
                // now render it
                let pos = getWorldPositionRelative updatedSprite
                let textureAndCell = getCellAndTexturePtr updatedSprite
                let srcCellRect = textureAndCell.Cell.AbsoluteCellBoundary
                let cellDestRect =
                    TSpriteRect(x = int(pos.X), y = int(pos.Y), w = srcCellRect.w, h = srcCellRect.h)

                //printfn "%A) Rendering '%A' #%A at Pos:%A(%A)); src=(%A, %A, %A, %A)/dest=(%A, %A, %A, %A)" spriteIndex updatedSprite.Name textureIndex updatedSprite.SpriteMatrix.Translation (getWorldPositionRelative updatedSprite)
                //        srcCellRect.x srcCellRect.y srcCellRect.w srcCellRect.h cellDestRect.x cellDestRect.y cellDestRect.w cellDestRect.h   // DELETE ME
                if SDL.SDL_RenderCopy(renderer, textureAndCell.TexturePtr, ref srcCellRect, ref cellDestRect) <> 0 then
                    SDL.SDL_LogError(int(SDL.SDL_LogCategory.SDL_LOG_CATEGORY_RENDER), sprintf "Unable to render sprite #%A (texture #%A) - %A" spriteIndex textureIndex (SDL.SDL_GetError()))
                updatedSprite
            )
        //printfn "Draw end (%A)" updatedSprites.Length   // DELETE ME
        updatedSprites

    /// get HotPoint position in World coordinate, used for positioning
    /// Note that HotPoint may not be upper left corner of the sprite cell, but
    /// rather usually at bottom center of the cell mainly for animation reason.
    /// For example, imaging X-flipping a player icon based on hotpoint at upper left
    /// corner of the cell versus when the hotpoint is at the center of the cell, and
    /// the sprite is at the edge wall.  The flipping may cause the sprite to get embedded
    /// into the wall if the hotpoint is at the upper left corner.
    /// NOTE: The position is based on world position plus local (relative) position,
    ///       hence it returns as Vector rather than as sprite that would be updated.
    member this.GetWorldHotPoint sprite =
        getWorldPositionRelative sprite

    /// get HotPoint position in local position, used for positioning.
    /// Local position are the usually set to (X=0, Y=0, Z=0) for all sprites and parent sprites.
    /// Sub/child sprites will be relative position to parent sprite relative to parent HotPoint
    /// This is rarely called (or used) by users and used more by animation system for sprite
    /// tree structure.  Possibly can be useful for example the avatar sprite holds a sword in
    /// hand, and  you'd need to know the hotpoint of the sword relative to player avatar, so that
    /// a fireball can be emitted from the sword hotpoint relative to player avatar...
    member this.GetLocalHotPoint sprite =
        getLocalHotPoint sprite

    /// This method will translate based on local position.  For example, you'd like to translate
    /// 5 pixels right and 10 pixels down (X=5, Y=10, Z=0) from the current (world) position
    /// at (X=1000, Y=500, Z=0) which will translate to new position (X=1005, Y=510, Z=0)
    /// It has nothing to do with Sprite local position that are used for parent-child sprite
    /// tree structure.
    member this.SetWorldTranslateLocal sprite (localPositionVector: Vector3) =
        let pos = getWorldPositionRelative sprite
        let newPos =
            Vector3(pos.X + localPositionVector.X, pos.Y + localPositionVector.Y, pos.Z + localPositionVector.Z)
        let mutable m = sprite.SpriteMatrix
        m.Translation <- newPos
        let newSprite = { sprite with SpriteMatrix = m }
        newSprite

    /// Sets the HotPoint's translation position in World coordinates
    member this.SetWorldTranslate sprite (absoluteWorldPositionVector: Vector3) =
        let mutable m = sprite.SpriteMatrix
        m.Translation <- absoluteWorldPositionVector
        let newSprite = { sprite with SpriteMatrix = m }
        newSprite

    /// XFlip toggle
    member this.XFlip sprite: TSprite =
        // get current orientation and calculate new radian value
        let radian = getRadian sprite.SpriteMatrix
        let flip = radian + (convertDegreesToRadian 180.0)
        rotateAbs sprite (float32 flip)

    /// YFlip toggle
    member this.YFlip sprite: TSprite =
        // get current orientation and calculate new radian value
        let radian = getRadian sprite.SpriteMatrix
        let newRadian = radian + (convertDegreesToRadian 270.0)
        rotateAbs sprite (float32 newRadian)

    /// Rotate in radian angle
    member this.RotateRad sprite radian: TSprite =
        rotateAbs sprite radian

    /// Rotate in degrees angle
    member this.RotateDeg sprite degrees: TSprite =
        let radian = convertDegreesToRadian degrees
        rotateAbs sprite (float32 radian)

    /// Scale
    member this.Scale sprite xScale yScale: TSprite =
        scale sprite xScale yScale 1.0f
    /// Uniform scaling without deformation on dimension ratio
    member this.ScaleUniform sprite xyScale: TSprite =
        scale sprite xyScale xyScale 1.0f

    /// Set Velocity=0
    member this.StopSpeed sprite =
        setVelocity sprite Vector3.Zero

    /// Set Acceleration=0
    member this.StopAccel sprite =
        setAcceleration sprite Vector3.Zero

    /// Set both a=0, v=0
    member this.Stop sprite =
        let mutable s = setVelocity sprite Vector3.Zero
        setAcceleration s Vector3.Zero

    /// Velocity
    member this.WorldVelocity sprite: Vector3 =
        sprite.Velocity

    /// setting world velocity
    member this.SetWorldVelocity sprite speed =
        setVelocity sprite speed

    /// adding delta (neg/pos) world velocity - recommend using Accel instead!
    member this.AddWorldVelocity sprite deltaV =
        let newSpeed =
            Vector3.Add(sprite.Velocity, deltaV)
        setVelocity sprite newSpeed

    /// Acceleration
    member this.WorldAccel sprite: Vector3 =
        sprite.Acceleration

    /// setting world velocity
    member this.SetWorldAccel sprite accel =
        setAcceleration sprite accel

    /// adding delta (neg/pos) acceleration - recommend stopping first or setting
    /// absolute acceleration rather than decelerating/accelerating
    member this.AddWorldAccel sprite deltaA =
        let newAccel =
            Vector3.Add(sprite.Acceleration, deltaA)
        setAcceleration sprite newAccel

    member this.SetPhysicsCallback sprite callback =
        {sprite with Physics = callback}
