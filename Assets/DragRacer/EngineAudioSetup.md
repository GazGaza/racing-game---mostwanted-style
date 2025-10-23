# Engine Audio Setup

1. **Prepare a loopable clip**
   - Use a mono recording that loops cleanly with minimal silence at the start/end.
   - Export as 16-bit PCM WAV or Ogg Vorbis at 44.1 kHz; both import cleanly into Unity and stay lightweight.

2. **Create the AudioSource**
   - Add an empty child GameObject under the car root and name it `EngineAudio`.
   - Add an `AudioSource` component, assign the clip, and enable **Loop**.
   - Set **Spatial Blend** between `0` (2D) and `1` (3D) based on camera distance; 0.6–0.8 works well for racing games.

3. **Assign to the controller**
   - Drag the AudioSource into the `engineAudioSource` field on `HybridSplineCarController` in the inspector.
   - Optionally tweak doppler level (0–0.5) to prevent extreme pitch shifts at high speeds.

4. **Test in play mode**
   - Accelerate, decelerate, and brake to make sure the scripted pitch/volume feedback matches the car behaviour.
   - Adjust `idleEnginePitch`, `maxEnginePitch`, and volume fields for your specific clip to taste.
