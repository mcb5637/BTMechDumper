using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BattleTech;
using Harmony;
using BattleTech.UI;
using HBS;
using UnityEngine;

namespace BTMechDumper
{
    [HarmonyPatch(typeof(SimGameState), "SetSimRoomState")]
    class SimGameState_SetSimRoomState
    {
        public static void Prefix(SimGameState __instance, DropshipLocation state)
        {
            if (state==DropshipLocation.MECH_BAY && Input.GetKey(KeyCode.LeftShift))
            {
                BTMechDumper.DumpData(__instance);
            }
        }
    }
}
