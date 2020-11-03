// Orinal Author: HidekiAI@users.noreply.github.com
// Project: git@github.com:HidekiAI/sdl2-fs
// Caveat: Read my README.md on why anti-type inferences is practiced here...
namespace sdlfs

open System
open MathNet.Numerics.LinearAlgebra // matrix (as object) and its accompanying maths
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
type SpriteMatrix = Matrix<float>

type SpriteVector = Vector<float>
type SpritePoint = Vector<float>
type SpriteRect = { Width: uint16; Height: uint16 }

// Future considerations:
// * for textures bitmap/atlas that could not be loaded (i.e. bigger than 64K pixels) should be assigned with annoyingly bright magenta textures
type SpriteCell =
    { ID: uint32 // 1-based ID, 0 means ghost sprite (has collision rect of 0-pixel, useful for animation to use blank)
      TextureID: uint32 // 1-based ID, you'll have to use this.to lookup where the origin of texture/atlas file was/is from; if this.is 0, but has CollisionRect, you can use it as an invisible sprite for something like invisible walls
      RelativeBoundary: SpriteRect // mainly used as dimension of the sprite for clipping, but it is also power of 2 (8, 16, 32, etc)
      RelativeCollisionRect: SpriteRect // has to be equal and/or less than the Boundary rect
      RelativeHotPoint: SpritePoint }

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
type SpriteFrame =
    {
      // wish there is unsigned float, we cannot allow negative FPS!
      FPS: float // i.e. 1.5 means it will render for 1.5 seconds before switching to next frame
      SpriteID: uint32 } // 1-based SrpiteCell.ID

type SpriteAnimation =
    | Frame of SpriteFrame
    | Loop of uint32

// One animation per sprite; there are some sprites that are from same SpriteCells shared for each Animations
type Sprite =
    { ID: uint32 // 1-based ID, Not to be confused from SpriteCell.ID (unlike SpriteCell.ID==0, this.ID can never be 0!  if it is 0, it is just a place-holder of a "null sprite" - usually because Texture cannot be found any more)

      Cells: SpriteCell []
      AbsolutePosition: SpritePoint // this.is where the HotPoints lands, NOT the upper left corner of each sprites, unless that's where the HotPoints of each sprites are
      // Scale, orientation, and translation
      SpriteMatrix: SpriteMatrix

      // One Animation sequence per Sprite
      Animation: SpriteAnimation []
      StepDirection: int8 // +1 to loop forward, 0 to not loop at all (if multiple SpriteID[] exist, always at SpriteID[0]), -1 to loop backwards

      // With Acceleration and Velocity, it can deal with primitive Newtonian physics
      Velocity: SpriteVector
      Acceleration: SpriteVector
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
type SpriteFileSprite =
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

type SpriteFileAnimationAnimationFrame = { ID: uint32; FPS: float }

type SpriteFileAnimationAnimation =
    { Frames: SpriteFileAnimationAnimationFrame []
      StepDirection: int8 // can go positive/negative
      RepeatLoopIndex: uint32 }

type SpriteFileAnimation =
    { Name: string
      Animation: SpriteFileAnimationAnimation }

type SpriteFile =
    { Image: string
      Sprites: SpriteFileSprite []
      Animations: SpriteFileAnimation [] }

///////////////////////////////////////////////////////////// Sprite File
type SpriteBuilder(maxSprites) =
    // ID's do not need to be static, it can stay internal to each sprite systems
    // but any sprites added to each (this.X) sprite system should not collide/overlap
    // on IDs
    let mutable spriteCellID: uint32 = 0u
    let mutable spriteID: uint32 = 0u
    // disposable list...
    let mutable textures: IntPtr [] = Array.zeroCreate maxSprites
    let mutable textureIndex: int = 0

    let mapTextureIndexToID =
        textures
        |> Array.mapi (fun index pTexture -> {| Index = index; Texture = pTexture |})

    let loadAnimation file =
        let json =
            System.IO.File.ReadAllLines(file)
            |> fun s -> String.Join(String.Empty, s)

        JsonConvert.DeserializeObject<SpriteFile>(json)

    let buildSpriteCell (animSprite: SpriteFileSprite) textureId =
        { ID = animSprite.ID
          TextureID = textureId
          RelativeBoundary =
              { Width = animSprite.Width
                Height = animSprite.Height }
          RelativeCollisionRect =
              { Width = animSprite.CollsionWidth
                Height = animSprite.CollisionHeight }
          RelativeHotPoint =
              vector [ float (animSprite.HotPointX)
                       float (animSprite.HotPointY)
                       0.0 ] }

    let buildSprite spriteId (spriteFileSprite: SpriteFileAnimation) (spriteCells: SpriteCell []) animations =
        { ID = spriteId
          Cells = spriteCells
          AbsolutePosition = vector [ 0.0; 0.0; 0.0; 0.0 ]
          Animation = animations
          StepDirection = spriteFileSprite.Animation.StepDirection
          Velocity = vector [ 0.0; 0.0; 0.0; 0.0 ]
          Acceleration = vector [ 0.0; 0.0; 0.0; 0.0 ]
          DeleteWhenOffScreen = false
          SleepWhenOffScreen = true
          SoundID = 0u
          SpriteMatrix =
              matrix [ [ 1.0; 0.0; 0.0; 0.0 ]
                       [ 0.0; 1.0; 0.0; 0.0 ]
                       [ 0.0; 0.0; 1.0; 0.0 ]
                       [ 0.0; 0.0; 0.0; 1.0 ] ] }

    let buildAnimFrame baseSpriteId (frame: SpriteFileAnimationAnimationFrame option) (loop: uint32 option) =
        match frame, loop with
        | Some f, None ->
            let ff =
                { FPS = f.FPS
                  SpriteID = f.ID + baseSpriteId }

            SpriteAnimation.Frame(ff)
        | None, Some l -> SpriteAnimation.Loop(l)
        | None, None -> failwith "Either frame or loop data has to be valid"
        | Some _, Some _ -> failwith "Cannot have both looping and frame info to be populated"

    let load window renderer (spriteAnimationJson: SpriteFile): Sprite [] =
        let surface =
            libSDLWrapper.getSurfaceBitMap window renderer false spriteAnimationJson.Image // assum this to throw, so it'll bail out if this fails to load

        // persist texture (because they need to be disposed on shutdown) and incremnt (global) textureIndex
        let texture =
            if not (surface.Equals(IntPtr.Zero))
            then libSDLWrapper.getTexture window renderer surface false
            else failwith "Unable to load Sprite atlas file"

        textures.[textureIndex] <- texture
        textureIndex <- textureIndex + 1

        let spriteCells =
            spriteAnimationJson.Sprites
            |> Array.mapi (fun spriteIndex sprite -> buildSpriteCell sprite (uint32 (textureIndex - 1))) // use the TextureID of last loaded texture

        let buildAnimation spriteFileAnimationAnaimation: SpriteAnimation [] =
            spriteFileAnimationAnaimation.Frames
            |> Array.map (fun frame -> buildAnimFrame spriteCellID (Some frame) None)
            |> fun a ->
                let loop =
                    buildAnimFrame spriteCellID None (Some spriteFileAnimationAnaimation.RepeatLoopIndex)

                // make sure looping frame is at the tail of the animation sequences
                Array.concat [ a; [| loop |] ]

        // Each Sprite contains one sequences of SpriteAnimation
        let buildAnimationSprites (sprites: SpriteFileSprite []) (animations: SpriteFileAnimation []) =
            animations
            |> Array.mapi (fun spriteIndex animSprite ->
                buildAnimation animSprite.Animation
                |> buildSprite (uint32 (spriteID + uint32 (spriteIndex))) animSprite spriteCells)

        let sprites =
            buildAnimationSprites spriteAnimationJson.Sprites spriteAnimationJson.Animations

        spriteID <- spriteID + uint32 (sprites.Length)
        sprites // TODO: Possibly persist this and expose `this.GetSprites` member?  IMHO if the caller loses scope of this array, it's their fault!

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
        loadAnimation jsonFilename |> load window renderer
