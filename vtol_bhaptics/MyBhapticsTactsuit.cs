using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using MelonLoader;

namespace MyBhapticsTactsuit
{
    public class TactsuitVR
    {
        /* A class that contains the basic functions for the bhaptics Tactsuit, like:
         * - A Heartbeat function that can be turned on/off
         * - A function to read in and register all .tact patterns in the bHaptics subfolder
         * - A logging hook to output to the Melonloader log
         * - 
         * */
        public bool suitDisabled = true;
        public bool systemInitialized = false;
        public float random_rumble_engine_intensity = 0.0F;
        public float random_rumble_surface_intensity = 0.0F; 
        public int heartbeat_pause = 1000; //ms
        public bool random_rumble_engine_active = false;
        public bool random_rumble_surface_active = false;

        // Events to start and stop threads
        private static ManualResetEvent HeartBeat_mrse = new ManualResetEvent(false);
        private static ManualResetEvent RandomRumbleEngine_mrse = new ManualResetEvent(false);
        private static ManualResetEvent RandomRumbleSurface_mrse = new ManualResetEvent(false);

        // dictionary of all feedback patterns found in the bHaptics directory
        public Dictionary<String, FileInfo> FeedbackMap = new Dictionary<String, FileInfo>();

        private static bHaptics.RotationOption defaultRotationOption = new bHaptics.RotationOption(0.0f, 0.0f);

        public void HeartBeatFunc()
        {
            while (true)
            {
                // Check if reset event is active
                HeartBeat_mrse.WaitOne();
                bHaptics.SubmitRegistered("HeartBeat");
                Thread.Sleep(heartbeat_pause);
            }
        }

        public void ElementWiseMultiply(byte[] arr, float x)
        {
            for(int i = 0; i < arr.Length; i++)
            {
                byte temp = arr[i];
                temp = (byte)(temp * ((5/255) + 0.5)); //Clip to range [0,5]
                temp = (byte)((temp * x) + 0.5); //Change intesity based on x
                arr[i] = temp;
            }
        }

        public void RandomRumbleFunc(ref ManualResetEvent mrse,ref float intensity, bHaptics.PositionType position)
        {
            while (true)
            {
                // Check if reset event is active
                mrse.WaitOne();
                Random rnd = new Random();
                byte[] arr = new byte[20];
                rnd.NextBytes(arr);
                ElementWiseMultiply(arr, intensity);
                if(Thread.CurrentThread.ManagedThreadId == 1)
                {
                    LOG("arr: " + String.Join(" ",arr));
                }
                bHaptics.Submit("Bytes", position, arr,50);
                Thread.Sleep(50);
            }
        }

        public void StartRandomRumbleEngine()
        {
            RandomRumbleEngine_mrse.Set();
            random_rumble_engine_active = true;
        }

        public void StopRandomRumbleEngine()
        {
            RandomRumbleEngine_mrse.Reset();
            random_rumble_engine_active = false;
        }

        public void StartRandomRumbleSurface()
        {
            RandomRumbleSurface_mrse.Set();
            random_rumble_surface_active = true;
        }

        public void StopRandomRumbleSurface()
        {
            RandomRumbleSurface_mrse.Reset();
            random_rumble_surface_active = false;
        }

        public TactsuitVR()
        {
            LOG("Initializing suit");
            if (!bHaptics.WasError)
            {
                suitDisabled = false;
            }
            RegisterAllTactFiles();
            LOG("Starting HeartBeat thread...");
            Thread HeartBeatThread = new Thread(HeartBeatFunc);
            HeartBeatThread.Start();
            LOG("Starting RandomRumbleEngine thread...");
            Thread RandomRumbleEngineThread = new Thread(() => RandomRumbleFunc(ref RandomRumbleEngine_mrse,ref random_rumble_engine_intensity, bHaptics.PositionType.VestBack));
            RandomRumbleEngineThread.Start();
            LOG("Starting RandomRumbleSurface thread...");
            Thread RandomRumbleSurfaceThread = new Thread(() => RandomRumbleFunc(ref RandomRumbleSurface_mrse, ref random_rumble_surface_intensity, bHaptics.PositionType.All));
            RandomRumbleSurfaceThread.Start();
        }

        public void LOG(string logStr)
        {
#pragma warning disable CS0618 // remove warning that the logger is deprecated
            MelonLogger.Msg(logStr);
#pragma warning restore CS0618
        }



        void RegisterAllTactFiles()
        {
            // Get location of the compiled assembly and search through "bHaptics" directory and contained patterns
            string configPath = Directory.GetCurrentDirectory() + "\\Mods\\bHaptics";
            DirectoryInfo d = new DirectoryInfo(configPath);
            FileInfo[] Files = d.GetFiles("*.tact", SearchOption.AllDirectories);
            for (int i = 0; i < Files.Length; i++)
            {
                string filename = Files[i].Name;
                string fullName = Files[i].FullName;
                string prefix = Path.GetFileNameWithoutExtension(filename);
                // LOG("Trying to register: " + prefix + " " + fullName);
                if (filename == "." || filename == "..")
                    continue;
                string tactFileStr = File.ReadAllText(fullName);
                try
                {
                    bHaptics.RegisterFeedbackFromTactFile(prefix, tactFileStr);
                    LOG("Pattern registered: " + prefix);
                }
                catch (Exception e) { LOG(e.ToString()); }

                FeedbackMap.Add(prefix, Files[i]);
            }
            systemInitialized = true;
        }

        public void PlaybackHaptics(String key, float intensity = 1.0f, float duration = 1.0f)
        {
            //LOG("Trying to play");
            if (FeedbackMap.ContainsKey(key))
            {
                //LOG("ScaleOption");
                bHaptics.ScaleOption scaleOption = new bHaptics.ScaleOption(intensity, duration);
                //LOG("Submit");
                bHaptics.SubmitRegistered(key, key, scaleOption, defaultRotationOption);
                // LOG("Playing back: " + key);
            }
            else
            {
                LOG("Feedback not registered: " + key);
            }
        }

        public void PlayBackHit(String key, float xzAngle, float yShift)
        {
            // two parameters can be given to the pattern to move it on the vest:
            // 1. An angle in degrees [0, 360] to turn the pattern to the left
            // 2. A shift [-0.5, 0.5] in y-direction (up and down) to move it up or down
            bHaptics.ScaleOption scaleOption = new bHaptics.ScaleOption(1f, 1f);
            bHaptics.RotationOption rotationOption = new bHaptics.RotationOption(xzAngle, yShift);
            bHaptics.SubmitRegistered(key, key, scaleOption, rotationOption);
        }

        public void StartHeartBeat()
        {
            HeartBeat_mrse.Set();
        }

        public void StopHeartBeat()
        {
            HeartBeat_mrse.Reset();
        }

        public bool IsPlaying(String effect)
        {
            return bHaptics.IsPlaying(effect);
        }

        public void StopHapticFeedback(String effect)
        {
            bHaptics.TurnOff(effect);
        }

        public void StopAllHapticFeedback()
        {
            StopThreads();
            foreach (String key in FeedbackMap.Keys)
            {
                bHaptics.TurnOff(key);
            }
        }

        public void StopThreads()
        {
            // Yes, looks silly here, but if you have several threads like this, this is
            // very useful when the player dies or starts a new level
            StopHeartBeat();
        }


    }
}