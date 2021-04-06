﻿
using System.Collections.Generic;
using System.Linq;
using ParallelTasks;
using VRage.Input;

namespace DefenseShields
{
    using Support;
    using Sandbox.ModAPI;
    using VRage.Game.ModAPI;
    using System;
    using VRage.Game.Entity;
    using VRage.Game;
    using Sandbox.Game.Entities;

    public partial class Session
    {
        public string ModPath()
        {
            var modPath = ModContext.ModPath;
            return modPath;
        }

        public bool TaskHasErrors(ref Task task, string taskName)
        {
            if (task.Exceptions != null && task.Exceptions.Length > 0)
            {
                foreach (var e in task.Exceptions)
                {
                    Log.Line($"{taskName} thread!\n{e}");
                }

                return true;
            }

            return false;
        }

        private void PlayerConnected(long id)
        {
            try
            {
                if (Players.ContainsKey(id))
                {
                    if (Enforced.Debug >= 3) Log.Line($"Player id({id}) already exists");
                    return;
                }
                MyAPIGateway.Multiplayer.Players.GetPlayers(null, myPlayer => FindPlayer(myPlayer, id));
            }
            catch (Exception ex) { Log.Line($"Exception in PlayerConnected: {ex}"); }
        }

        private void PlayerDisconnected(long l)
        {
            try
            {
                IMyPlayer removedPlayer;
                Players.TryRemove(l, out removedPlayer);
                PlayerEventId++;
                if (Enforced.Debug >= 3) Log.Line($"Removed player, new playerCount:{Players.Count}");
            }
            catch (Exception ex) { Log.Line($"Exception in PlayerDisconnected: {ex}"); }
        }

        private bool FindPlayer(IMyPlayer player, long id)
        {
            if (player.IdentityId == id)
            {
                Players[id] = player;
                PlayerEventId++;
                if (Enforced.Debug >= 3) Log.Line($"Added player: {player.DisplayName}, new playerCount:{Players.Count}");
            }
            return false;
        }
        private void UpdateControlKeys()
        {
            if (ControlRequest == ControlQuery.Keyboard) {

                MyAPIGateway.Input.GetListOfPressedKeys(_pressedKeys);
                if (_pressedKeys.Count > 0 && _pressedKeys[0] != MyKeys.Enter)
                {
                    var firstKey = _pressedKeys[0];
                    Settings.ClientConfig.UpdateKey(firstKey, _lastKeyAction, UiInput);
                    ControlRequest = ControlQuery.None;
                    Settings.VersionControl.UpdateClientCfgFile();
                    MyAPIGateway.Utilities.ShowNotification($"{firstKey.ToString()} is now the shield {_lastKeyAction} Key", 10000);
                }
            }
        }

        private void ChatMessageSet(string message, ref bool sendToOthers)
        {
            var somethingUpdated = false;

            if (message == "/ds" || message.StartsWith("/ds "))
            {
                switch (message)
                {

                    case "/ds remap action":
                        ControlRequest = ControlQuery.Keyboard;
                        somethingUpdated = true;
                        _lastKeyAction = "action";

                        MyAPIGateway.Utilities.ShowNotification($"Press the key you want to use for the shield Action key - Current: {Settings.ClientConfig.ActionKey.ToString()}", 10000);
                        break;
                    case "/ds remap left":
                        ControlRequest = ControlQuery.Keyboard;
                        somethingUpdated = true;
                        _lastKeyAction = "left";
                        MyAPIGateway.Utilities.ShowNotification($"Press the key you want to use for the shield Left key - Current: {Settings.ClientConfig.Left.ToString()}", 10000);
                        break;
                    case "/ds remap right":
                        ControlRequest = ControlQuery.Keyboard;
                        somethingUpdated = true;
                        _lastKeyAction = "right";

                        MyAPIGateway.Utilities.ShowNotification($"Press the key you want to use for the shield Right key - Current: {Settings.ClientConfig.Right.ToString()} ", 10000);
                        break;
                    case "/ds remap front":
                        ControlRequest = ControlQuery.Keyboard;
                        somethingUpdated = true;
                        _lastKeyAction = "front";

                        MyAPIGateway.Utilities.ShowNotification($"Press the key you want to use for the shield Front key - Current: {Settings.ClientConfig.Front.ToString()}", 10000);
                        break;
                    case "/ds remap back":
                        ControlRequest = ControlQuery.Keyboard;
                        somethingUpdated = true;
                        _lastKeyAction = "back";

                        MyAPIGateway.Utilities.ShowNotification($"Press the key you want to use for the shield Back key - Current: {Settings.ClientConfig.Back.ToString()}", 10000);
                        break;
                    case "/ds remap up":
                        ControlRequest = ControlQuery.Keyboard;
                        somethingUpdated = true;
                        _lastKeyAction = "up";

                        MyAPIGateway.Utilities.ShowNotification($"Press the key you want to use for the shield Up key - Current: {Settings.ClientConfig.Up.ToString()}", 10000);
                        break;
                    case "/ds remap down":
                        ControlRequest = ControlQuery.Keyboard;
                        somethingUpdated = true;
                        _lastKeyAction = "down";

                        MyAPIGateway.Utilities.ShowNotification($"Press the key you want to use for the shield Down key - Current: {Settings.ClientConfig.Down.ToString()}", 10000);
                        break;
                    case "/ds hud vertical":
                        ControlRequest = ControlQuery.Mouse;
                        somethingUpdated = true;
                        _lastKeyAction = "mouse";

                        MyAPIGateway.Utilities.ShowNotification($"Press the mouse button you want to use to open and close the Shield Menu", 10000);
                        break;
                }

                if (ControlRequest == ControlQuery.None)
                {

                    string[] tokens = message.Split(' ');

                    var tokenLength = tokens.Length;
                    if (tokenLength > 1)
                    {
                        switch (tokens[1])
                        {
                            case "TEMP1":
                                {
                                    int maxDrawCount;
                                    if (tokenLength > 2 && int.TryParse(tokens[2], out maxDrawCount))
                                    {
                                        //Settings.ClientConfig.MaxProjectiles = maxDrawCount;
                                        var enabled = maxDrawCount != 0;
                                        //Settings.ClientConfig.ClientOptimizations = enabled;
                                        somethingUpdated = true;
                                        //MyAPIGateway.Utilities.ShowNotification($"The maximum onscreen projectiles is now set to {maxDrawCount} and is Enabled:{enabled}", 10000);
                                        Settings.VersionControl.UpdateClientCfgFile();
                                    }

                                    break;
                                }
                            case "TEMP2":
                                //Settings.ClientConfig.ShowHudTargetSizes = !Settings.ClientConfig.ShowHudTargetSizes;
                                somethingUpdated = true;
                                //MyAPIGateway.Utilities.ShowNotification($"Shipsize icons have been set to: {Settings.ClientConfig.ShowHudTargetSizes}", 10000);
                                Settings.VersionControl.UpdateClientCfgFile();
                                //FovChanged();
                                break;
                        }
                    }
                }

                if (!somethingUpdated)
                {
                    if (message.Length <= 3)
                        MyAPIGateway.Utilities.ShowNotification("Valid DefenseShield Commands:\n '/ds remap'  -- Remap keys\n '/ds hud'  -- Modify Hud elements\n '/ds info' -- Get general information", 10000, "Red");
                    else if (message.StartsWith("/ds hud"))
                        MyAPIGateway.Utilities.ShowNotification($"Hold Action key ({Settings.ClientConfig.ActionKey}) and use arrow keys to move hud\n Hold Action key ({Settings.ClientConfig.ActionKey}) and use +/- keys to change scale of hud", 10000, "Red");
                    else if (message.StartsWith("/ds remap"))
                        MyAPIGateway.Utilities.ShowNotification("'/ds remap action'  -- Remaps Action key (default numpad0)\n '/ds remap noshunt'  -- Remaps NoShunting key (numpad5)\n '/ds remap left'  -- Remaps Left shield key (default numpad4)\n '/ds remap right'  -- Remaps Right shield key (default numpad6)\n '/ds remap forward'  -- Remaps Forward shield key (default numpad8)\n '/ds remap backward'  -- Remaps Backward shield key (default numpad2)\n '/ds remap up'  -- Remaps Up shield key (default numpad9)\n '/ds remap down'  -- Remaps Down shield key (default numpad1)\n", 10000, "Red");
                    else if (message.StartsWith("/ds info"))
                        MyAPIGateway.Utilities.ShowNotification("Short key presses toggle shunting state for that direction only\n Long presses toggles shunting for all directions\n", 10000, "Red");
                }
                sendToOthers = false;
            }
        }

        internal void GenerateButtonMap()
        {
            var ieKeys = Enum.GetValues(typeof(MyKeys)).Cast<MyKeys>();
            var keys = ieKeys as MyKeys[] ?? ieKeys.ToArray();
            var kLength = keys.Length;
            for (int i = 0; i < kLength; i++)
            {
                var key = keys[i];
                KeyMap[key.ToString()] = key;
            }

            var ieButtons = Enum.GetValues(typeof(MyMouseButtonsEnum)).Cast<MyMouseButtonsEnum>();
            var buttons = ieButtons as MyMouseButtonsEnum[] ?? ieButtons.ToArray();

            var bLength = buttons.Length;
            for (int i = 0; i < bLength; i++)
            {
                var button = buttons[i];
                MouseMap[button.ToString()] = button;
            }
        }

        private void MenuOpened(object obj)
        {
            InMenu = true;
        }

        private void MenuClosed(object obj)
        {
            InMenu = false;
        }

        private void SplitMonitor()
        {
            foreach (var pair in CheckForSplits)
            {
                if (WatchForSplits.Add(pair.Key))
                    pair.Key.OnGridSplit += GridSplitWatch;
                else if (Tick - pair.Value > 120)
                    _tmpWatchGridsToRemove.Add(pair.Key);
            }

            for (int i = 0; i < _tmpWatchGridsToRemove.Count; i++)
            {
                var grid = _tmpWatchGridsToRemove[i];
                grid.OnGridSplit -= GridSplitWatch;
                WatchForSplits.Remove(grid);
                CheckForSplits.Remove(grid);
            }
            _tmpWatchGridsToRemove.Clear();

            foreach (var parent in GetParentGrid)
            {
                ParentGrid oldParent;
                if (Tick - parent.Value.Age > 120)
                    GetParentGrid.TryRemove(parent.Key, out oldParent);
            }
        }

        #region Events

        internal struct ParentGrid
        {
            internal MyCubeGrid Parent;
            internal uint Age;
        }

        private void GridSplitWatch(MyCubeGrid parent, MyCubeGrid child)
        {
            GetParentGrid.TryAdd(child, new ParentGrid { Parent = parent, Age = Tick });
        }

        private void OnEntityRemove(MyEntity myEntity)
        {
            if (Environment.CurrentManagedThreadId == 1) {

                MyProtectors protector;
                if (GlobalProtect.TryGetValue(myEntity, out protector)) {

                    foreach (var s in protector.Shields.Keys) {

                        ProtectCache cache;
                        if (s.ProtectedEntCache.TryRemove(myEntity, out cache))
                            ProtectCachePool.Return(cache);
                    }
                    EntRefreshQueue.Enqueue(myEntity);
                }
            }
        }

        private void OnSessionReady()
        {
            SessionReady = true;
        }
        #endregion
    }
}
