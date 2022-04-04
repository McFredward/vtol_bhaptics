using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MelonLoader;
using HarmonyLib;
using UnityEngine;
using MyBhapticsTactsuit;

namespace vtol_bhaptics
{
    public class vtol_bhaptics : MelonMod
    {
        public static TactsuitVR tactsuitVr;

        public override void OnApplicationStart()
        {
            base.OnApplicationStart();
            tactsuitVr = new TactsuitVR();
            tactsuitVr.PlaybackHaptics("HeartBeat");
        }

        //----Constant vibrations based on different values-----
        // Coannot play in parallel, so surface vibrations have priority (because its more intense)
        [HarmonyPatch(typeof(VehicleMaster), "Update", new Type[] { })]
        public class bhaptcs_engine
        {
            private static float current_thrust = 0.0F; //in range [0,255]
            private static float surface_speed = 0.0F;

            [HarmonyPostfix]
            public static void Postfix(VehicleMaster __instance)
            {
                //-Constant Vibrations on all devices based on the current speed ON THE GROUND-
                if (__instance.flightInfo.isLanded && __instance.flightInfo.surfaceSpeed > 5) //plain is on the ground
                {
                    if(!tactsuitVr.random_rumble_surface_active)
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
                    tactsuitVr.random_rumble_surface_active=false;
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
                else if(__instance.engines.Length > 1 && !tactsuitVr.random_rumble_surface_active)
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
                            tactsuitVr.random_rumble_engine_active=false;
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

                //Based on the current thrust, the vibrations-itensity differs
                //tactsuitVr.LOG("current thrust: " + current_thrust.ToString());
                //tactsuitVr.LOG("surface speed: " + __instance.flightInfo.surfaceSpeed.ToString());
                tactsuitVr.random_rumble_engine_intensity = current_thrust/1000;
                tactsuitVr.random_rumble_surface_intensity = surface_speed/255;
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
                //tactsuitVr.LOG(string.Format("({0} collided with {1})", (object)col.contacts[0].thisCollider.gameObject.name, (object)col.contacts[0].otherCollider.gameObject.name));
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
                if(__instance.flightInfo.playerGs > (double) __instance.maxG)
                {
                    double distance = __instance.flightInfo.playerGs - (double)__instance.maxG;
                    if(distance > 0 && distance < 10)
                    {
                        tactsuitVr.heartbeat_pause = 1000;
                    }
                    else if(distance > 10 && distance < 25)
                    {
                        tactsuitVr.heartbeat_pause = 800;
                    }
                    else if(distance > 25)
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
        [HarmonyPatch(typeof(VTOLCollisionEffects), "Health_OnDamage", new Type[] {typeof(float), typeof(Vector3), typeof(Health.DamageTypes) })]
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
    }
}
