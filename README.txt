This repository contains the Unity project and related materials for the full paper "I Can Feel It Coming in VR Tonight: Haptic-Only Spatial Target Selection for Blind and Visually Impaired Users"

Abstract:
While haptic feedback often supplements other senses, its effectiveness as a standalone guidance modality in virtual reality (VR) for blind and visually impaired (BVI) users is unknown. This paper presents the first empirical evaluation of unimodal haptic spatial target acquisition in VR, using a Fitts' Law paradigm with 32 BVI participants guided by a vibrotactile vest. Results revealed a significant directional speed-accuracy trade-off: lateral movements were faster but less accurate than for depth-based targets. Both continuous quadratic and intermittent pulse-based growth modulation demonstrated similar effectiveness, emphasising user preference variability. High error rates and reasonable Fitts’ Law model fits indicate that classic predictive models of movement fail to capture the complex behaviour of non-visual VR interaction. Qualitative insights reinforce this, showing experience is dominated by personal preference and perceived difficulty, not technical specifications. Overall, the findings support the viability of haptic guidance in VR but highlight the need for adaptive mechanisms.

Project Overview:
This Unity project contains the virtual reality environment and experimental setup described in the paper. It was created to investigate how blind and visually impaired users select targets with haptic feedback from a bHaptics vest.

The primary scene, Fitts.unity, includes the complete experimental procedure, virtual environments, and data logging components.

System Requirements:
Unity Version: 2022.3.47f1

Target Platform: Meta Quest with Link Cable

Hardware:
VR Headset: Meta Quest 2

PC: Windows 10/11

Dependencies & Plugins
This project relies on the following packages and plugins. Please ensure they are installed correctly.

Unity Packages:
Meta XR All-in-One SDK
bHaptics Haptic Plugin

How to Run the Experience
Connect Your VR Headset: Ensure your headset is connected to your computer and recognised (e.g., via Oculus Link).

Enter Play Mode: Press the "Play" button at the top of the Unity Editor.

Controls:

[Controller Grip Button]: Begin test

[Controller Trigger Button]: Select Target