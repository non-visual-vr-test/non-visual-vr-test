This repository contains the Unity project and related materials for the paper "Vibrotactile-Only Virtual Reality Target Selection for Visually Impaired Users"

Abstract:
While vibrotactile cues often supplement visual or auditory information, their effectiveness as a standalone sensory substitution channel in virtual reality (VR) for blind and visually impaired (BVI) users is unknown. We present the first empirical evaluation of unimodal vibrotactile spatial target acquisition in peripersonal space across lateral and depth axes in VR. We conducted a study with 32 seated BVI participants who used a vibrotactile vest for guidance and a handheld controller for contact feedback and target selection. Results revealed a significant axis-dependent speed-accuracy trade-off: lateral selections were faster but less accurate than depth-based selections. High error rates and a counter-intuitive error pattern, where participants were significantly less accurate for closer targets, reveal the exceptional difficulty of the task. While qualitative feedback indicated high participant enthusiasm and potential psychological benefits, the high error rates, particularly at close range, demonstrate that standard unimodal vibrotactile guidance from off-the-shelf hardware is insufficient for high-precision targeting without supplementary braking cues or higher-fidelity actuators. We conclude by highlighting the need for alternative feedback mechanisms and BVI-specific VR design guidelines.

Project Overview:
This Unity project contains the Unity virtual reality environment and experimental setup described in the paper. It was created to investigate how blind and visually impaired users select targets with haptic feedback from a bHaptics vest and handheld controller.

The primary scene, Fitts.unity, includes the complete experimental procedure, virtual environments, and data logging components.

System Requirements:
Unity Version: 2022.3.47f1

Target Platform: Meta Quest with Link Cable

Hardware:
VR Headset: Meta Quest 2, Pro, 3

PC: Windows 11

Dependencies & Plugins
This project relies on the following packages and plugins. Please ensure they are installed correctly.

Unity Packages:
Meta XR All-in-One SDK
bHaptics Haptic Plugin

How to Run the Experience
Connect Your VR Headset: Ensure your headset is connected to your computer and recognised (e.g., via Link cable).

Select the configurations for controller (left or right), target layout, and number of trials per set.

Enter Play Mode: Press the "Play" button at the top of the Unity Editor. Re-centre the view via the Quest menu to correctly position yourself with the midpoint 55cm in front of the HMD.

Move your hand forward from your midpoint 55cm to reach the midpoint (controller will vibrate while making contact)

Press the grip button to initiate vest vibrations and generate the first target pair.

Move your hand in the direction indicated by the vest vibration location. Once on target, the controller will vibrate. Press the trigger button to select the current target.

Move along the current movement axis to the opposite target. Continue moving and selecting until vest vibrations stop. Re-start process by moving to midpoint and pressing the grip button to initiate the new target pair.

Once all sets are complete, the program will automatically exit and the logs file will open on your connected computer.

Controls:

[Controller Grip Button]: Begin test/initiate vibrations from the vest

[Controller Trigger Button]: Select Target
