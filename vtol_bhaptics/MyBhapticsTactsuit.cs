using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using ModLoader;
using Bhaptics.Tact;
using UnityEngine;

namespace MyBhapticsTactsuit
{
    public class TactsuitVR
    {
        /* A class that contains the basic functions for the bhaptics Tactsuit, like:
         * - A Heartbeat function that can be turned on/off
         * - A function to read in and register all .tact patterns in the Bhaptics.Tact subfolder
         * - A logging hook to output to the Melonloader log
         * - 
         * */
        public bool suitDisabled = true;
        public bool systemInitialized = false;
        public float random_rumble_engine_intensity = 0.0F;
        public float random_rumble_surface_intensity = 0.0F; 
        public int heartbeat_pause = 1000; //ms
        public bool heartbeat_active = false;
        public bool random_rumble_engine_active = false;
        public bool random_rumble_surface_active = false;
        public bool misc_active = false;
        public int misc_function_no = -1;

        // Events to start and stop threads
        private static ManualResetEvent HeartBeat_mrse = new ManualResetEvent(false);
        private static ManualResetEvent RandomRumbleEngine_mrse = new ManualResetEvent(false);
        private static ManualResetEvent RandomRumbleSurface_mrse = new ManualResetEvent(false);
        private static ManualResetEvent Misc_mrse = new ManualResetEvent(false);

        // dictionary of all feedback patterns found in the Bhaptics.Tact directory
        public Dictionary<String, FileInfo> FeedbackMap = new Dictionary<String, FileInfo>();

        //Init bHaptic API
        public static HapticPlayer bhaptics = new HapticPlayer("7bb21f11-6675-4e25-ab78-e420a6b129c2", "VTOLVR");
        //private static RotationOption defaultRotationOption = new RotationOption(0.0f, 0.0f);
        

        public void HeartBeatFunc()
        {
            while (true)
            {
                // Check if reset event is active
                HeartBeat_mrse.WaitOne();
                bhaptics.SubmitRegistered("HeartBeat");
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

        public void RandomRumbleFunc(ref ManualResetEvent mrse,ref float intensity, Bhaptics.Tact.PositionType position)
        {
            while (true)
            {
                // Check if reset event is active
                mrse.WaitOne();
                System.Random rnd = new System.Random();
                byte[] arr = new byte[20];
                rnd.NextBytes(arr);
                ElementWiseMultiply(arr, intensity);
                if(Thread.CurrentThread.ManagedThreadId == 0)
                {
                    Debug.Log("arr: " + String.Join(" ",arr));
                }
                bhaptics.Submit("Bytes", position, arr,50);
                Thread.Sleep(50);
            }
        }

        //MiscThread is doing some small tasks, which are not meant to be played in parallel
        //misc_function_no decides what task to do
        //misc_function_no = 0 -> Stall Warning
        public void MiscFunc(ref int _misc_function_no)
        {
            Misc_mrse.WaitOne();
            if(_misc_function_no == 0) //Stall Warning
            {
                //PlaybackHaptics("Stall1");
                //PlaybackHaptics("Stall2");
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

        public void StartMisc()
        {
            Misc_mrse.Set();
            misc_active = true;
        }

        public void StopMisc()
        {
            Misc_mrse.Reset();
            misc_active = false;
        }

        public TactsuitVR()
        {
            Debug.Log("Initializing suit");
            RegisterAllTactFiles();
            Debug.Log("Starting HeartBeat thread...");
            Thread HeartBeatThread = new Thread(HeartBeatFunc);
            HeartBeatThread.Start();
            Debug.Log("Starting RandomRumbleEngine thread...");
            Thread RandomRumbleEngineThread = new Thread(() => RandomRumbleFunc(ref RandomRumbleEngine_mrse,ref random_rumble_engine_intensity, Bhaptics.Tact.PositionType.VestBack));
            RandomRumbleEngineThread.Start();
            Debug.Log("Starting RandomRumbleSurface thread...");
            Thread RandomRumbleSurfaceThread = new Thread(() => RandomRumbleFunc(ref RandomRumbleSurface_mrse, ref random_rumble_surface_intensity, Bhaptics.Tact.PositionType.All));
            RandomRumbleSurfaceThread.Start();
            Debug.Log("Starting Misc thread...");
            Thread MiscThread = new Thread(() => MiscFunc(ref misc_function_no));
            //Dont use the Misc Thread at the moment
            //MiscThread.Start();
        }


        void RegisterAllTactFiles()
        {
            // Get location of the compiled assembly and search through "Bhaptics.Tact" directory and contained patterns
            string configPath = Directory.GetCurrentDirectory() + "\\VTOLVR_ModLoader\\mods\\Bhaptics_integration\\bHaptics";
            DirectoryInfo d = new DirectoryInfo(configPath);
            FileInfo[] Files = d.GetFiles("*.tact", SearchOption.AllDirectories);
            for (int i = 0; i < Files.Length; i++)
            {
                string filename = Files[i].Name;
                string fullName = Files[i].FullName;
                string prefix = Path.GetFileNameWithoutExtension(filename);
                Debug.Log("Trying to register: " + prefix + " " + fullName);
                if (filename == "." || filename == "..")
                    continue;
                string tactFileStr = File.ReadAllText(fullName);
                try
                {
                    bhaptics.RegisterTactFileStr(prefix, tactFileStr);
                    Debug.Log("Pattern registered: " + prefix);
                }
                catch (Exception e) { 
                    Debug.Log(e.ToString());
                                    }

                FeedbackMap.Add(prefix, Files[i]);
            }
            systemInitialized = true;
        }

        public void PlaybackHaptics(String key, float intensity = 1.0f, float duration = 1.0f)
        {
            Debug.Log("Trying to play");
            if (FeedbackMap.ContainsKey(key))
            {
                Debug.Log("ScaleOption");
                Bhaptics.Tact.ScaleOption scaleOption = new Bhaptics.Tact.ScaleOption(intensity, duration);
                Debug.Log("Submit");
                bhaptics.SubmitRegistered(key, scaleOption);
                //bhaptics.SubmitRegistered(key, key, scaleOption, defaultRotationOption);
                Debug.Log("Playing back: " + key);
            }
            else
            {
                Debug.Log("Feedback not registered: " + key);
            }
        }

        public void StartHeartBeat()
        {
            HeartBeat_mrse.Set();
            heartbeat_active = true;
        }

        public void StopHeartBeat()
        {
            HeartBeat_mrse.Reset();
            heartbeat_active = false;
        }

        public bool IsPlaying(String effect)
        {
            return bhaptics.IsPlaying(effect);
        }

        public void StopHapticFeedback(String effect)
        {
            bhaptics.TurnOff(effect);
        }

        public void StopAllHapticFeedback()
        {
            StopThreads();
            foreach (String key in FeedbackMap.Keys)
            {
                bhaptics.TurnOff(key);
            }
        }

        public void StopThreads()
        {
            if (random_rumble_engine_active)
            {
                StopRandomRumbleEngine();
                Debug.Log("RandomRumble stopped");
            }
            if (random_rumble_surface_active)
            {
                StopRandomRumbleSurface();
            }
            if (misc_active)
            {
                StopMisc();
            }
        }


    }
}