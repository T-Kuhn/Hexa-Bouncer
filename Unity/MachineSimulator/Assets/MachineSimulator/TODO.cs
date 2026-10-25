// - [X] cleanup code
// - [X] Add a thing that let's us quickly switch between which solution is used
// - [X] make our current arm work no matter how it is rotated (calculations need to be in local space)
// - [X] make our current arm work no matter where it is placed
// - [X] would be nice if we had gizmo lines for all joints maybe?
// - [X] also make upper joint move (keep this one parallel to floor)
// - [X] automatically instanciate arms at sensible positions
//     - [X] instantiate end effector platform on play
//     - [X] instantiate 6 arms on play (setup target positions inside end effector platform)
//     - [X] parameters for easy experimentation:
//           - distance from center for motors pairs at midpoint
//           - distance apart of motor pair
//           - distance from center for target pairs below endeffector at midpoint
//           - distance apart of target pair
// - [X] Fix joints-can't-handle-tilt-problem (need more degrees of freedom for top most joint)
//     - [X] add that debugGizmo method -> (origin, redDir, greenDir, blueDir)
//     - [X] pass in end effector (hexaplate) tilt
//     - [X] FirtStep: Calculate correct realTarget according to tilt
//     - [X] SecondStep: Calculate correct joint angles for joints 4 and 5 according to endEffector tilt
// - [X] Fix last joint not rotating together with tilted platform problem

// - [X] send 6 values instead of 4 to microcontroller
// - [X] handle 6 values and 6 sineSteppers on microcontroller
// - [X] add two more OUTPUTS (for the additional two motors) to the constants file on the microcontroller
// - [X] connect the 6 stepper controllers accordingly
// - [X] Add UI buttons to calibrate each motor (need to adjust rotation in small steps)
// - [X] Add _levelingOffset which is a LLMachineState that will accumulate calibration values for all the motors
// - [X] For normal move instructions, add them to the _levelingOffset to get the real target machine state
// - [X] Need to set the machineState after above calibration as the origin position for the machine
// - [X] All following diff-instructions should be relative to the origin position
// - [X] Add UI buttons to do the following: up/down at different speeds
// - [X] TEST WITH ARMS CONNECTED
// Things we now know after that test:
// - [X] test with NO-feedback-loop (no rotary encoder) stepper driver settings
// - [X] we probably need to put something soft under the arms on startup. Some arms are pushing agains the ground because of the way steppers work.
// - [X] Add apply-calibration button (applies all the values we found to be ideal for each motor)
// - [X] we also want a button that makes the machine go up/down continuously. maybe 10 times with pauses in between (maybe multiple pause intervals?)
// - [X] we want to go up/down even faster
// - [X] we want to go further up and down to a position above origin

// - [ ] We need a way to move the simulatedMachine with HighLevelInstructions.
//     - [X] HighLevelInstruction needs to contain: PlateCenterPosition, PlateRotationQuaternion, MoveTime
//     - [X] We Need a "add pose" button
//     - [X] First goal will be to be able to play-back the recorded HighLevelInstruction
//     - [X] Introduce the concept of a "sequence" (a list of HighLevelInstructions). This will be useful when converting the HighLevelInstructions to stringed LowLevelInstructions.
//     - [X] Add "Playback Stringed" button. When pressed...
//         - [X] ...Create stringed linear HighLevelInstructions in intervals of 0.1 * moveTime for all HighLevelInstructions in the sequence.
//              - [X]  To create the stringed instructions, the IK has to be executed every time the HexaPlate's position changes. Use an Observable to let the IK know that it needs to update.
//              - [X]  Make sure above Observable fires whenever we update the HexaPlate's position/rotation.
//              - [X] After this, it should work like this: Update Hexaplate position/rotation -> IK updates -> We are able to read out all the motor rotations and create a stringed LowLevelInstruction from them.
//         - [X] ...playback the sequence of stringed HighLevelInstructions
//     - [X] add capability to independently log position data every frame. Log all the positions in the CSV format in order to make graphs to check if the linear stringed instructions work as expected.
//     - [X] our logging is a bit shabby; We might get a different number of data points for different playbacks. Fix this. (also log time instead of frame count?)
//     - [X] Move logger in seperate class
//     - [X] Do the same kind of logging for motor rotations (stringed vs unstringed)
//     - [ ] make sure the motorRotations match what the microcontroller is expecting
//           - [X] correct cw/ccw direction
//                - [X] for M1 moving-arm-up-rotation-direction is minus
//                - [X] for M2 moving-arm-up-rotation-direction is plus
//           - [X] correct 0-position (need to check rotation at origin position and use it as offset)
//           - [X] We need to multiply our theta by something to scale the value to the one the microcontroller expects
//     - [X] For some reason, creating stringed instructions playback isn't working with many HLInstructions
//     - [X] Also reset rotation on Teleport to origin.
//     - [X] Add LowLevelMachineStateProvider. Arms need to update their MotorRotation to LowLevelMachineStateProvider everytime the IK executes.
//         - [X] Use the LowLevelMachineState from above provider to do make the LowLevelInstructions.
//     - [X] When creating the stringed HighLevelInstructions, also create LowLevelInstructions by checking the motor position of all the motors after kicking off the IK.
//     - [X] Add button called "Playback stringed on RM"
//     - [X] Make stringed Linear LowLevelInstructions work on the microcontroller (currently we use cos to start/stop smoothly. We will not need that anymore).
//     - [X] Next, create stringed LowLevelInstructions from the sequence (go through data in pairs: "from" HLInstruction, "to" HLInstruction).
//           - To generate them, we move time forward in small steps, check how far each of the motors has moved and create a LowLevelInstruction for each step.
// - [X] The stringed together instructions can basically be executed as linear-speed movements to target pos. We DO of course use a sine-based movement when moving the end effector
//       when generating motor rotations with IK, but the movement commands themselves can be linear speed movements to target position because they will be very short and about 50 for a complete move.
//       The shortness and number of linear-speed moves will ensure that in totality, a smooth movement can be achieved.
// - [X] refactor "SineStepper/SineStepperController" code to "LinearStepper/LinearStepperController" etc.
// - [X] add "Speed x1", "Speed x2", "Speed x3" buttons to the sequencing UI
// - [X] check why "apply offset" -> "stringed playback (up and down to origin)" results in a endstate that is not the same as after apply offset (how did we define origin?)
// - [X] is it because the first apply offset wasn't sent correctly? do we need to send 0,0,0,0,0,0 the first time after startup?
//       -> Yes
// - [X] speed is too fast.
// - [X] need to change machineModel parameters to match real machine (distance from center for motors and targets)
// - [X] test tilting/rotating/translating (don't go too far though, we now know that there are instable states where an arm might flap downwards)
// - [X] Add LoadSequenceFromCode button to the sequencing UI
// - [X] Add manual set-time inspector UI for HexaPlateMover
//     - [X] checkbox to switch between automatic and manual time setting
//     - [X] slider to set time (0 to 10)
// - [X] add a way to make generation of stringed circle-tilt/circle-translation commands possible
//     - [X] Need a "CreateListOfStringedInstructionsFromMoveStrategy" method
//         - [X] pass in "startTime", "endTime", "strategy" and "stringedInstructionsPerSecond".
//               strategy will be executed from startTime to endTime with the specified number of instructions per second.
// - [X] need an easy way to measure distance to ground
//     - some sort of gameObject we can move around which will show distance in inspector?
// - [X] do the circle tilt!
// - [X] we want a way to do the circle tilt continuously
//     - [X] add "Playback Async" and "Playback Async On Machine" buttons
//     - [X] Buttons need to execute code that resembles what we wrote below in approach A
// - approach A:
//   - 1. send command to go to start position
//   - 2. send command to do one circle tilt after a small delay
//   - 3. after a small delay which is almost exactly how long it takes the machine to perform 1 circle tilt: send commands for another circle tilt
//   - 4. repeat 3 a few times
// A seems ideal since - if we ever happen to do anything with this machine - it will be in a way similar to this approach (e.g. ball juggling will use a similar approach)
// - [X] rendering performance optimization. Too many drawcalls (shadowcaster/receive shadows/too many seperate meshes);
//     - [X] Merge some meshes.
//     - [X] Simplify meshes (holes are too complex. Could be simplified with vertex-reduction)
//     - [X] Export meshes as .obj files (this is to future proof our approach; we might want to use the meshes in the raspberry-pi-Godot-appraoch at some point in time)
//     - [X] Replace arm 0.4 left/right and base with simplified components that use .obj files
//     - [X] Take After screenshot for comparison
// - [X] Add FPS counter to the UI so that we can get an idea of how efficient our rendering is.
// - [X] Make a new video showcasing all the moves we can do now at this point in time
//      - [X] add a sequenceFromCode which showcases all the moves one after another
//      - tiltCircle/translateCircle/turningAroundYAxis
// - [X] streamline the way we currently string together multiple moves in SequenceFromCode
//       (add HelperMethods to be able to write instructions more concisely)
// - [X] play around with cameras and decide how to mount them (absolute vs relative tracking)
//     - [X] we'd also need to make sure the two USB cables are flexible enough to move together with the end-effector.
//     - [/] IF we decide to mount them on the base-frame (absolute tracking), then we need to
//           make sure the cameras will never get hit by the moving end-effector.
// - [X] test camera we already have
// - [X] Attach racket to machine
// - [X] test ball throwing movement with ball on a little lever a bit away from end-effector-triangle
//     -> Tested. Didn't work satisfactorily.
// - [X] Babysteps towards working multi-camera-image-processing:
//     - [X] initialize both cameras and then expose two functions to get a frame from each camera.
//         - [X] add a parameter to control which camera to get with getCamera() (0 or 1)
//         - [X] save the intPtr for both cameras and set parameters for both (maybe a second UVCCameraPlugin instance is cleanest?)
//     - [X] show the image data for both cameras?
//     - [X] we suspect the 120FPS multi-camera-streaming is currently not working because it's just too much data to put through the same USB 3.0 bus.
//           See what happens if we use different USB controllers for each camera.
//           -> fixed by adding PCI-E USB 3.0 controller
//     - [X] investigate reaccuring 70ms load peaks
//         - they happen with image acquisition
//         - [X] check if the same peaks still occur when...
//             - [X] ...we only use one camera instead of two
//                   -> disabling cam2: still get peaks on cam1 stream
//                   -> disabling cam1: still get peaks on cam2 stream
//             - [X] ...we set the cameras to 60FPS instead of 120FPS
//                   -> we still get peaks but now the peak is 110ms long
// - [X] ultimately, we do not need to get to the root of above load-peak-problem. We can work around it.
//     - [X] workaround would include getting image data on different threads. Threads just copy frame data into newest data buffer.
//     - [X] Unity runs on 120FPS and uses newest data (maybe includes timestamp so we have a way of knowing whether the data is old or not)
//     - [X] check if getting rid of the image data conversion will improve performance enough so that it's worth the trouble (should be)
//         - [X] log timestamp data inside the plugin getCameraTexture() function before and after conversion.
// - [X] add a log when the image retrieval takes longer than expected (e.g. longer than 50ms)
// - [X] add custom shader to display OpenCVs BGR pixel data correctly
// - [X] maybe add a counter next to the FPS counter on the top of the screen.
//       The counter should show how many times the image retrieval took longer than 50ms for each camera (show like this "<cameraId>:<count>")
// - [X] make sure auto white balance gets turned off on initialization
// - [X] add image processing to find position ping pong ball in image data (re-use approach used in octo-bouncer project?)
//     - check "BallDataFromPixelBoarders"
//     - also note that we only care for a certain area above the paddle, so there's a min y value we can savely ignore.
//     - pixel brightness should be good enough
// - [X] maybe adjust camera angles so that they are roughly the same
// - [X] Check: can we get raw camera data (UYVY) via OpenCV?
//     - [X] nope. data gets converted to BGR internally. We can only get that BGR data.
// - [X] work on double-camera-image-processing (work on nice buffering) 
// - [X] test old camera (See3CAM_CU135) to see whether we'd get smooth results with that one.
// - [X] occasional-70ms-image-aqcuisition-waiting-time-problem
//     - [X] We could see if using the extension API and setting the FPS control to 100FPS changes anything
//          -> nope.
//     - [X] Do the ping pong ball swing back and forth in front of camera for 3 mins. See if any of the 70ms peaks occur...
//          - [X] ...when using the e-CAMview app
//              -> Yep!
//          - [X] ...our OpenCV plugin && Unity app
//              -> Yep!
//     - [/] we could stop using OpenCV and instead write a native DirectShow plugin and see if that helps
//          - [X] first standalone app
//          - [X] set exposure to -7
//          - [/] then standalone app that can be opened two times for both cameras
//          - [/] than convert the app into a Unity plugin
//     - [/] use our balldetection to continiously extract ball position of swinging ball.
//           See if the continiouty of the data is OK
//          - [/] ...with new frameGrabber
//          - [/] ...with old OpenCV based plugin (maybe. If we feel like we want to check and compare)
//     - [/] crucial piece of information: From what we can tell, the problem appears in Unity
//           (even when only using one camera at 60 FPS) but NOT when we use e-CAMview. This suggests that
//           there IS a correct way to interface with the camera without the problem occuring.
//     - [/] other information: Interestingly, if we try to get exposure, gain, contrast through our OpenCV plugin,
//           we only get the camera default values (instead the ones we set and which are clearly applied)
//           Not sure if this has something to do with the occasional 70ms load peaks we are seeing.
//     - [X] write email explaining situation with camera and 70ms peak problem
// - [X] gather swinging ball pixel position data (ball position per frame)
//     - [X] both with synced apprach...
//     - [X] and thread-based image acquisition approach
// - [X] FrameGrabber data seems way nicer. So let's use FrameGrabber instead of OpenCV to get frames from the camera.
// - [X] Now that we have nice continuous data (excluding the 70ms-gaps-problem), we can go ahead and do some more testing:
//     - [X] test with two cameras
//     - [X] log ball positoin data simoultaneously with two cameras
// - [X] Add the camera mount to the end-effector hexaplate model
// - [x] shoot rays from the camera mounts in accordance with ball pixel position data
// - [X] update end-effector model so that it contains camera holder
// - [X] read BallDataFromPixelBorders and see if we still agree with the approach.
// - [X] Calculate ball position from rays
//     - [X] plane-intersect-approach to get 3D ball position from two camera ball position data
//     - [X] absolute position in world space
// - [X] draw ball visualization showing where the camera thinks the ball is
// - [X] do a simple "go-up-if-ball-gets-below-certain-height" test
//    - [X] move up as soon as ball gets below 0.3 height
// - [X] we should be able to whack ball up
// - [/] start next measurement phase after machine is in start position again
//    - we measure all the time independent of the MachineLoop. This is better.
// - [X] we should get continuous bouncing until the ball fails to hit the paddle
//    - confirmed
// - [X] gather ball position data while machine is bouncing ball
// - [X] get a simple PID/PD controller going to keep the ball on the paddle
// - [X] with high speed moves, we aren't getting smooth virtual machine playback (fix).
// - [X] gather new ball data with the new ball bouncing code (should bounce higher now, maybe?)
// - [X] we need to ensure that our ball data is as continuous as possible.
// - [x] we'll need to make sure that we can playback motion commands on virtual machine
//       in sync to real machine to get accurate world space ball position.
// - [X] calculate ball velocity with linear regression / gradient descent
// - [X] our velocity is sampleCount/2 * timestep late (10/2 * 8ms = 40ms). So we need to add ball velocity that
//       will happen in the next 40ms and add that to our "current" ball velocity to get real time ball velocity.
// - [X] log this new real time ball velocity to see whether or not it checks out.
// - [X] we should not only look at ball height for up-down-motion-trigger, but also current ball velocity
//       (are we still moving up or already moving down?)
//       Adding this will allow us to time the bounces better -> use slower/more elegant moves.
//     - [X] We have no excuse to NOT accurately calculate predicted ball hits paddle time. It's too easy. No excuse.
//       It has to be done.
//     - [X] we can even add this prediction to our log and check if it's accurate or not after recording some data.
// - [X] Add a UI that shows current rest position height
// - [X] add rest-height-increase to upDownMotion method
// - [X] apply rest height increase to the down-motion so that we can stepwise increase the rest height
// - [X] use up and down keys to increase/decrease rest height (track the accumolated changes and make sure they are always positive)
//     - Add a seperate class for this, should be similar to the ModeSwitcher class. 
//     - just creating the class and a [SerializedField] to setup the reference is enough. I will Add the class as a Component to a gameObject in the scene and set it up in the inspector manually.
// - [X] so if the ball is bouncing and we are pressing up, that specific bounce will end up at a higher height (we are incresing the rest height)
// - [X] check with virtual machine if it's working correctly or not
// - [X] check with real machine if it's working correctly or not
// - [X] We need some interesting ways to juggle the ball.
//     - [X] 150ms move command with 0.2 height
//     - [X] then smoothly (or suddenly?) change to 225ms move time with 0.23 height
//     - [X] every second bounce it a hit style
//     - [X] need a way to easily switch between different hit styles (so we can switch with keyboard keys)
//     - [X] Add offset to left/right with keys (for debug/testing)
//     - [X] juggle ball while trying to keep paddle centered below ball
//     - [X] maybe try to move paddle to center of current ball position when going back down
//     - [X] maybe do a slight screw motion while going up just for funsies?

// ↓ Do this!
// - [ ] add some more subtitles (evo)!
// - [ ] add text scramble animation (evo) to year titles?
// - [ ] maybe make first "chopstick" subtitle stick around for a bit longer?
// - [ ] Implement I-component of PID controller to 

// - [ ] 2.5 Minutes of Bouncing
// - [ ] 2.5 Minutes of Slow-Motion Bouncing 
// - [ ] thumnbnails

// - [ ] Video footage of (maybe improved?) flex.
// - [ ] Video footage of bouncing (go through some modes)
//     - [ ] with screen capture as well
// lighting too dark. need to redo.








// - [/] Implement integral component of PID controller.
//       ball sometimes has tendency to stay on right side of paddle for a long time. I component will fix that.
//      -> we decided that this isn't really needed (ball isn't constantly hanging around one side of the paddle)


// Name: Hexa-Bouncer

// - [ ] we could try to get rid of ball-flickering with averages (if both data streams are relatively fresh)


// - [ ] make thick cross where the imageProcessing thinks the ball is.
// - [ ] experiment: ball at different heights, how many pixels per 2cm?
// - [ ] update README pic
// - [ ] check why async playback on virtual machine is not smooth at all (especially on high speeds)

// - [ ] We decide to mount them at the end-effector (relative tracking), then we need
//       to make sure we can reliably get a synced up machine state from the virtual machine
//       in order to be able to calculate the absolute position of the ping pong ball.

// - [ ] need to decide how we want to implement ball tracking:
//     - [ ] A: use two cameras connected to PC via USB, do image processing in unity on the PC
//         - advantages:
//             - image processing can be screen recorded
//             - we can be certain that this approach will work (no hardware/resource limitations)
//             - simple
//         - disadvantages:
//             - if we end up going the raspberry-pi-image processing && Godot route, we end up buying the raspberry pi hardware with cameras anyway, so we will not need to USB cameras anymore.
//             - need to be careful to not oversaturate the USB bus.
//      - [ ] B: use a raspberry pi 5 to do two-camera-image-processing and get ball position via USB serial
//         - advantages:
//             - we can sync our global shutter cameras to take pictures at the exact same time (the syncing isn't really crucial though, just a nice to have)
//             - raspberry pi
//             - simplicity will be back once we are able to fully commit to GoDot/image processing route (and no PC will be needed anymore)
//         - disadvantages:
//             - simplicity is kinda lost (PC, Teensy, raspberry pi, ...)
//             - seems harder to implement/debug (especially once we commit on the GoDot route)

// Ball position sensing ideas:
// - [ ] use a camera + computer vision to track ball position
// - [ ] use a light barried setup
//     - maybe two tears so that we can get an idea where the ball is headed?
//     - sensor array needs to be equipped with many sensors for this to work accurately
//     - also looks a bit ugly (sensors all around the paddle)
// - [ ] We shine a IR light onto the ball from below. IR sensors measure how much light is reflected back.
//       The more light, the closer the ball is to the sensor. If we have multiple sensors, we can get an idea of the ball's position.
// - [ ] Many, many ToF sensors? vl53l1x?
//       - probably not ideal, since the ball is round and reflections are unreliable
// - [ ] fpga-based camera processing?
//     - [ ] maybe run unity-app on a raspberry pi without rendering much (use the dummy-display)?

// - [ ] At some stage I want to use a raspberry pi 5 to do both two-camera-image-processing and machine-control (IK).
//     - this will probably be a bit later though. Like a quest you do after the main quest.
//       could maybe do a video about porting the processing to the raspberry pi.

// - [ ] fix problem where we can not test the stringed command execution in Unity because the execution takes slightly longer than expected due to it's implementation
//     - maybe we could adjust the waitTime depending on how much longer it actually took until the next execution (so that little differences don't add up.)

// Thinking:

// Thinking about that mesh merging script.
// 1. Drop all the MeshFilters we want to merge into a public array on the MeshMerger component
// 2. Mesh merger component generates a merges mesh and saves it as an obj file.
// 3. we can use that mesh instead of the original meshes
// 4. create a new prefab with optimized (merged) meshes
// 5. get some sort of vertex-reduction going
// 6. again, build a new prefab with merged and reduced meshes

// - There's a slight problem with how the microcontroller handles moveCommands:
//     - The microcontroller remembers all the current motor positions and calculates the steps needed to reach a target rotation for each motor
//     - The problem is that with the slicing approach, if the machine isn't in the exact state we expect it to be in, the first moveCommand
//       might be at a way too high speed if the target position is too far away.
//     - I don't think there's a ideal solution to fix/improve this though.

// - [ ] create a tilt-strategy that actually tilts around the center of the hexaplate (instead of "around a point a bit above the hexa plate")

// - [ ] test with different stepper driver PID settings
//     -> this might be interesting, especially if we connect the driver to the setup software and look through all the settings,
//        but I'm not sure we'll get anything out of this (going no-feedback-loop might be the best option after all)

// - [ ] change colors of arm parts which are colliding so that we can get a better feeling of how the arm design has to be improved
// - [ ] add some sort of animation for end effector platform
//     - [X] moving up and down
//     - [X] moving left and right
//     - [X] moving forward and backward
//     - [X] circling (position)
//     - [X] circling + up/down (position)
//     - [X] tilting around the X-Axis
//     - [ ] tilting around the Z-Axis
//     - [ ] circling (tilt)
//     - [ ] moving up and down at left/right/front/back position
// - [ ] iterate on arm design based on what we'll see in the simulated machine
// - [ ] center gizmo would be nice, so we can tell how far away from center the hexaplate is
// - [ ] maybe switch project to the High Definition Render Pipeline (HDRP) with demo scene as base for better visual look
// - [ ] Need a name for the thing. Some ideas:
//   - u-joint. The UJ-Table
//   - hexapod. The Hexa Bot.

// ↓ We basically don't care enough for this right now... Probably will never go down this path.
// - [ ] fix slight stutter on new command received
// - the Serial command timing is unreliable. We send Serial data around +/-25ms
//   - So whenever we are too early, we cut off the current command which leads to the next command having to move further which causes a visible stutter
//   - And whenever we are too late the machine stops for a few milliseconds which also is not a good look
// Thinking about how to improve this problem:
//  - A: Adding a start/stop ramp instead of making the whole motion linear will improve the situation
//      - ultimately, we will probably want to move with sinosoidal speed curves anyway,
//        so fixing this problem probably shouldn't be super high priority
//      - However, it WOULD be nice to get the forever-circle-tilt work seamlessly, wouldn't it?
//  - B: Implement some sort of stack on the microcontroller's side will allow us to refill the stack with new
//       commands even before the microcontroller has finished the current round of commands
//      - Will not work forever since our stack will slowly grow in size
//      - However, since our final application will not use this feature extensively,
//        it doesn't need to work forever anyway

// Thing to check:
// - Wouldn't it also work if the 6 arms were placed one at a time at 60deg intervals instead of pairwise in 120deg intervals?


// HOW TO USE:
//     1. Press Unity Editor's play button
//     2. Press Apply Offset button; The physical machine should now be in exactly the same state as the virtual machine
//     3. Move HexaPlate, AddHLInstruction, Teleport to origin, AddHLInstruction, PlaybackStringed


// CHANGELOG
// - 2025-07-14: Tightened nuts on last joint (where the arm is connected to the hexaplate) for better rigidity.
// - 2025-07-14: No PID (open loop) seems better.

