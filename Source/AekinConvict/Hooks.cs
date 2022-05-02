using AekinConvict.Core;
using AekinConvict.Databases;
using System;
using System.Collections.Generic;

namespace AekinConvict.Hooks
{ 
    public abstract class HookState
    {
        public abstract bool OnLogin(NetPlayer plr, Account account);
        public abstract bool OnChat(NetPlayer plr, string msg);
        public abstract bool OnCommand(NetPlayer plr, string cmd);
    }

    public static class AekinHookManager
    {
        private static List<HookState> HookStates;

        internal static void Initialize()
        {
            HookStates = new List<HookState>();
        }

        internal static void AggregateHook(Action<HookState> action)
        {
            foreach (HookState hookState in HookStates)
                action(hookState);
        }

        public static void Hook(HookState state)
        {
            if (!HookStates.Contains(state))
                HookStates.Add(state);
        }
        public static void Rehook(HookState state)
        {
            if (HookStates.Contains(state))
                HookStates.Remove(state);

            HookStates.Add(state);
        }
        public static void Unhook(HookState state)
        {
            if (HookStates.Contains(state))
                HookStates.Remove(state);
        }
    }
}
