using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

using ModLoader;
using Harmony;
using UnityEngine;
using MyBhapticsTactsuit;
using VTOLVR.DLC.Rotorcraft;

namespace vtol_bhaptics
{
    public class Main : VTOLMOD
    {
        public static TactsuitVR tactsuitVr;
        private static HarmonyInstance instance = HarmonyInstance.Create("vtol.bhaptics");


        public override void ModLoaded()
        {
            VTOLAPI.SceneLoaded += SceneLoaded;
            base.ModLoaded();
            instance.PatchAll(Assembly.GetExecutingAssembly());
            tactsuitVr = new TactsuitVR();
            tactsuitVr.PlaybackHaptics("HeartBeat");
        }

        void Update()
        {
        }

        void FixedUpdate()
        {
        }

        private void SceneLoaded(VTOLScenes scene)
        {
            switch (scene)
            {
                case VTOLScenes.ReadyRoom:
                    //Add your ready room code here
                    break;
                case VTOLScenes.LoadingScene:
                    //Add your loading scene code here
                    break;
            }
        }

        //----Constant vibrations based on different values-----
        // Coannot play in parallel, so surface vibrations have priority (because its more intense)
        [HarmonyPatch(typeof(VehicleMaster), "Update", new Type[] { })]
        public class bhaptcs_engine
        {
            private static float current_thrust = 0.0F; //in range [0,255]
            private static float surface_speed = 0.0F;
            private static bool is_landed_saved = false; //To check if the vehicle hits the ground

            [HarmonyPostfix]
            public static void Postfix(VehicleMaster __instance)
            {
                //-Constant Vibrations on all devices based on the current speed ON THE GROUND-
                //Debug.Log(__instance.engines[1].finalThrust.ToString());
                
                if (__instance.flightInfo.isLanded && __instance.flightInfo.surfaceSpeed > 5) //plain is on the ground
                {
                    if (!tactsuitVr.random_rumble_surface_active)
                    {
                        tactsuitVr.StopRandomRumbleEngine();
                        tactsuitVr.StartRandomRumbleSurface();
                        tactsuitVr.random_rumble_surface_active = true;
                        tactsuitVr.random_rumble_engine_active = false;
                    }
                    surface_speed = __instance.flightInfo.surfaceSpeed;
                }
                else
                {
                    surface_speed = 0.0F;
                    tactsuitVr.StopRandomRumbleSurface();
                    tactsuitVr.random_rumble_surface_active = false;
                }

                //-Constant vibrations on the back of the vest based on the current thrust-
                if (__instance.engines[0].startedUp && !tactsuitVr.random_rumble_surface_active)
                {
                    if (!tactsuitVr.random_rumble_engine_active)
                    {
                        tactsuitVr.StartRandomRumbleEngine();
                        tactsuitVr.random_rumble_engine_active = true;
                    }
                    current_thrust = __instance.engines[0].finalThrust;
                }
                else if (__instance.engines.Length > 1 && !tactsuitVr.random_rumble_surface_active)
                {
                    if (__instance.engines[1].startedUp)
                    {
                        if (!tactsuitVr.random_rumble_engine_active)
                        {
                            tactsuitVr.StartRandomRumbleEngine();
                            tactsuitVr.random_rumble_engine_active = true;
                        }
                        current_thrust = __instance.engines[1].finalThrust;
                    }
                    else
                    {
                        if (tactsuitVr.random_rumble_engine_active)
                        {
                            tactsuitVr.StopRandomRumbleEngine();
                            tactsuitVr.random_rumble_engine_active = false;
                        }
                    }
                }
                //----If Pilot dies, pause all Threats----
                if (__instance.pilotIsDead)
                {
                    tactsuitVr.StopRandomRumbleEngine();
                    tactsuitVr.StopRandomRumbleSurface();
                    tactsuitVr.StopHeartBeat();
                }

                //----Short landing shock after hitting the ground----
                if(!is_landed_saved && __instance.flightInfo.isLanded) //From flase to true indicates collision
                {
                    float verticalSpeed = __instance.flightInfo.verticalSpeed;
                    //hardest collision I got was around -12
                    //Debug.Log("vertical hit speed: " + verticalSpeed.ToString());
                    float intensity = Math.Min(1.0F, Math.Max(0.0F, -verticalSpeed / 5));
                    //Debug.Log("intensity: " + intensity.ToString());
                    if(-verticalSpeed > 0.5)
                    {
                        tactsuitVr.PlaybackHaptics("Shock", intensity);
                    }
                }

                //Based on the current thrust, the vibrations-itensity differs
                //tactsuitVr.LOG("current thrust: " + current_thrust.ToString());
                //tactsuitVr.LOG("surface speed: " + __instance.flightInfo.surfaceSpeed.ToString());
                tactsuitVr.random_rumble_engine_intensity = current_thrust / 1000;
                tactsuitVr.random_rumble_surface_intensity = surface_speed / 255;

                //--- Helicopter DLC----

                if (__instance.isHelicopter) //Add a bit vibrations based on the rotationVelocity
                {
                   try
                    {
                        var t = Traverse.Create(__instance.mainRotor);
                        float rotationVelocity = (float)t.Field("rotationVelocity").GetValue(); //Get value of an protected value
                        //Debug.Log("rotationVelocity: " + rotationVelocity.ToString());
                        tactsuitVr.random_rumble_engine_intensity += rotationVelocity / 55000;
                        float dragTorque = __instance.mainRotor.GetDragTorque();
                        //Debug.Log("dragTorque: " + dragTorque.ToString());
                        tactsuitVr.random_rumble_engine_intensity += dragTorque / 450000;
                        //Debug.Log("rumble_intensty = " + tactsuitVr.random_rumble_engine_intensity.ToString());
                    }
                    catch (Exception e)
                    {
                        //Debug.Log("DLC FAILURE: " + e.Message);
                    }
                }


                is_landed_saved = __instance.flightInfo.isLanded;
            }
        }


        //----Collisions-----
        [HarmonyPatch(typeof(VTOLCollisionEffects), "OnCollisionEnter", new Type[] { typeof(Collision) })]
        public class bhaptcs_collision
        {
            [HarmonyPostfix]
            public static void Postfix(Collision col)
            {
                //TODO: set up the intesity based on the magnitude (what is the max maginitude?)
                //double magnitude = (double) col.impulse.magnitude; 
                //tactsuitVr.LOG(magnitude.ToString());
                //Debug.Log(string.Format("({0} collided with {1})", (object)col.contacts[0].thisCollider.gameObject.name, (object)col.contacts[0].otherCollider.gameObject.name));
                tactsuitVr.PlaybackHaptics("Collision");
            }
        }

        //----OverG Heartbeat----
        [HarmonyPatch(typeof(OverGWarning), "Update", new Type[] { })]
        public class bhaptcs_over_g
        {
            [HarmonyPostfix]
            public static void Postfix(OverGWarning __instance)
            {
                if (__instance.flightInfo.playerGs > (double)__instance.maxG)
                {
                    double distance = __instance.flightInfo.playerGs - (double)__instance.maxG;
                    if (distance > 0 && distance < 10)
                    {
                        tactsuitVr.heartbeat_pause = 1000;
                    }
                    else if (distance > 10 && distance < 25)
                    {
                        tactsuitVr.heartbeat_pause = 800;
                    }
                    else if (distance > 25)
                    {
                        tactsuitVr.heartbeat_pause = 500;
                    }
                    tactsuitVr.StartHeartBeat();
                }
                else
                {
                    tactsuitVr.StopHeartBeat();
                }
            }
        }
        //----Ejection seat----
        [HarmonyPatch(typeof(EjectionSeat), "Eject", new Type[] { })]
        public class bhaptics_ejection
        {
            [HarmonyPostfix]
            public static void Postfix(EjectionSeat __instance)
            {
                tactsuitVr.PlaybackHaptics("Eject");
                tactsuitVr.StopRandomRumbleEngine();
                tactsuitVr.StopRandomRumbleSurface();
                tactsuitVr.StopHeartBeat();
            }
        }

        //----Damage from Missíles------
        [HarmonyPatch(typeof(VTOLCollisionEffects), "Health_OnDamage", new Type[] { typeof(float), typeof(Vector3), typeof(Health.DamageTypes) })]
        public class bhaptics_missiles
        {
            [HarmonyPostfix]
            public static void Postfix(VTOLCollisionEffects __instance)
            {
                tactsuitVr.PlaybackHaptics("Collision");
            }
        }
        //-----Carrier Catapult engage------
        [HarmonyPatch(typeof(CarrierCatapult), "Hook", new Type[] { typeof(CatapultHook) })]
        public class bhaptics_carrier_hook
        {
            [HarmonyPostfix]
            public static void Postfix(CatapultHook hook)
            {
                try
                {
                    if (hook.GetComponentInParent<VehicleMaster>().actor.isPlayer)
                    {
                        tactsuitVr.PlaybackHaptics("CarrierHook");
                    }
                }
                catch (Exception e) { };
            }
        }

        /**
        //-----Stall------
        [HarmonyPatch(typeof(HUDStallWarning), "WarningRoutine", new Type[] { })]
        public class bhaptics_stall
        {
            [HarmonyPrefix]
            public static bool Prefix(HUDStallWarning __instance)
            {
                Debug.Log("WanringRoutine.enabled = " + __instance.enabled.ToString());
                if (__instance.enabled)
                {
                    if (tactsuitVr.misc_function_no != 0)
                    {
                        tactsuitVr.misc_function_no = 0;
                    }
                    if (!tactsuitVr.misc_active)
                    {
                        tactsuitVr.StartMisc();
                        Debug.Log("StartedMiscThread");
                    }
                }
                return true; //Tells Harmony to keep going in the original function
            }

            [HarmonyPostfix]
            public static void Postfix(HUDStallWarning __instance)
            {
                if (tactsuitVr.misc_active)
                {
                    tactsuitVr.StopMisc();
                    Debug.Log("StoppedMiscThread");
                }
            }
        }
        **/
    }
}
