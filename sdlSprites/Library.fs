// Orinal Author: HidekiAI@users.noreply.github.com
// Project: git@github.com:HidekiAI/sdl2-fs

namespace sdlfs

open System
open SDL2   // we'll be accessing SDL_Texture
open Newtonsoft.Json    // deserialization of Sprite file

type SpritePoint =
    {
      // NOTE: though screen coordinates are uint32, we'd want it to be float so that:
      //       * when doing physics such as acceleration (vector math) you can move Sprites at finer/smoother motion (i.e. if Velocity=1.5 pixels/frame, in 2 frames, it can move 3 relative pixels instead of 2)
      //       * having it to allow negative position, can allow the sprites to hide off-screen (use with caution)
      // Caveats:
      //       * when doing point-to-point comparisons, only use the integer portion, epsilon diff can be just messy and wasteful on CPU
      X: float
      Y: float
      // Note: Be careful with negative-Z issue on (relative to the) camera, some hardware drivers will clip early and may cause sprite to flicker!
      Z: float } // normally, this should stay as 0.0, but can be used to layer sprites front or behind

type SpriteVector = SpritePoint // directional vector, assume always relative to origin(0,0,0)

type SpriteRect =
    {
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
      Width: uint16
      Height: uint16 }

// Future considerations:
// * for textures bitmap/atlas that could not be loaded (i.e. bigger than 64K pixels) should be assigned with annoyingly bright magenta textures
type SpriteCell =
    { ID: uint32 // 1-based ID, 0 means ghost sprite (has collision rect of 0-pixel, useful for animation to use blank)
      TextureID: IntPtr // 1-based ID, you'll have to use this to lookup where the origin of texture/atlas file was/is from; if this is 0, but has CollisionRect, you can use it as an invisible sprite for something like invisible walls
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

type Sprite =
    { ID: uint32 // 1-based ID, Not to be confused from SpriteCell.ID (unlike SpriteCell.ID==0, this ID can never be 0!  if it is 0, it is just a place-holder of a "null sprite" - usually because Texture cannot be found any more)
      AbsolutePosition: SpritePoint // this is where the HotPoints lands, NOT the upper left corner of each sprites, unless that's where the HotPoints of each sprites are
      Scale: SpritePoint // will allow scaling in X and Y separate, though it may be inefficient, it's a very simple effect you can gain for almost free
      Animation: SpriteAnimation []
      StepDirection: int32 // +1 to loop forward, 0 to not loop at all (if multiple SpriteID[] exist, always at SpriteID[0]), -1 to loop backwards
      // With Acceleration and Velocity, it can deal with primitive Newtonian physics
      Velocity: SpriteVector
      Acceleration: SpriteVector
      // persistence
      DeleteWhenOffScreen: bool // when you start giving velocities to a sprite, it has the tendencies to just go and disappear...
      SleepWhenOffScreen: bool // possibly, all these  off-screen stuff can be D.U. based...
      // Sound associated to animation, only relevant when AnimationFreqecy is > 0, Loop != 0, and Sprites.Length > 1
      SoundID: uint32 } // 1-based ID, 0 means no sound

///////////////////////////////////////////////////////////// Sprite File
type SpriteFileSprite =
    {
        ID: uint32
        X: uint32
        Y: uint32
        Width: uint32
        Height: uint32
        HotPointX: uint32
        HotPointY: uint32
        CollisionX: uint32
        CollsionY: uint32
        CollsionWidth: uint32
        CollisionHeight: uint32
    }
type SpriteFileAnimationAnimationFrame =
    {
        ID: uint32
        FPS: float
    }
type SpriteFileAnimationAnimation =
    {
        Frames: SpriteFileAnimationAnimationFrame[]
        StepDirection: uint8
        RepeatLoopIndex: uint32
    }
type SpriteFileAnimation =
    {
        Name: string
        Animation: SpriteFileAnimationAnimation
    }
type SpriteFile =
    {
        Image: string
        Sprites: SpriteFileSprite[]
        Animations: SpriteFileAnimation[]
    }
///////////////////////////////////////////////////////////// Sprite File

module libSprite =
    let loadAnimation file =
        let json =
            System.IO.File.ReadAllLines(file)
        JsonConvert.DeserializeObject<SpriteFile>(json)

    let load window renderer (spriteAnimationJson: SpriteFile): Sprite [] =
        let surface =
            libSDLWrapper.getSurfaceBitMap window renderer false spriteAnimationJson.Image

        let texture =
            if surface.Equals(IntPtr.Zero) = false
                then libSDLWrapper.getTexture window renderer surface false
            else 
                failwith "Unable to load Sprite atlas file"

        if texture.Equals(IntPtr.Zero) = false then
            // we have an atlas, so build (at least one) sprites from it
            let mutable width = 0
            let mutable height = 0
            let mutable format = 0u
            let mutable access = 0
            match SDL.SDL_QueryTexture(texture, ref format, ref access, ref  width, ref height) with
            | 0 -> 
                let sprites =
                    for sprite in spriteAnimationJson.Sprites do
                        let spriteCell =
                            {
                                ID = sprite.ID;
                                TextureID = texture;
                                RelativeBoundary = {
                                    Width = sprite.Width;
                                    Height = sprite.Height;
                                };
                                RelativeCollisionRect = {
                                    Width = sprite.CollsionWidth;
                                    Height = sprite.CollisionHeight;
                                };
                                RelativeHotPoint = {
                                    X = sprite.HotPointX;
                                    Y = sprite.HotPointY;
                                    Z = 0;
                                }
                            }
                            let {
                                ID = 0;
                                AbsolutePosition = SpritePoint;
                                Scale = SpritePoint;
                                Animation = SpriteAnimation [];
                                StepDirection = 0;
                                Velocity = SpriteVector;
                                Acceleration = SpriteVector;
                                DeleteWhenOffScreen = false;
                                SleepWhenOffScreen = true ;
                                SoundID = 0;
                            }
                sprites
            | v -> 
                let strErr = SDL.SDL_GetError()
                let err =
                    sprintf "SDL_QueryTexture() - Unable to query texture for '%s' - %A: %s" spriteAnimationJson.Image v strErr
                SDL.SDL_Log(err)
                failwith err
        else
            failwith "Unable to load Sprite atlas file"
