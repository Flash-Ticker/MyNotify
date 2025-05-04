using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MyNotify", "RustFlash", "1.0.0")]
    [Description("A modern notification system for Rust")]
    public class MyNotify : RustPlugin
    {
        #region Felder

        private const string LAYER_NAME = "UI.MyNotify";
        private static MyNotify _instance;
        private readonly Dictionary<ulong, NotificationManager> _playerNotifications = new Dictionary<ulong, NotificationManager>();
        
        // Optional: Referenz zu ImageLibrary für benutzerdefinierte Bilder
        [PluginReference] private Plugin ImageLibrary;

        private class NotificationData
        {
            public string Message;
            public int Type;
            public string Id = CuiHelper.GetGuid();
            public float CreationTime;
        }

        // Berechtigungen mit korrektem Plugin-Namenspräfix
        private const string 
            PermSeeNotify = "mynotify.see",
            PermNotify = "mynotify.notify",
            PermPlayerNotify = "mynotify.player",
            PermAllPlayersNotify = "mynotify.allplayer";

        #endregion

        #region Konfiguration

        private ConfigData _config;

        private class ConfigData
        {
            public string DisplayType = "Overlay"; // Overlay oder Hud
            public float Height = 50f;
            public float Width = 260f;
            public float XMargin = 20f;
            public float YMargin = 5f;
            public float YStartPosition = -50f;
            public bool ShowAtTopRight = true;
            public float DefaultDuration = 5f;
            public int MaxNotificationsOnScreen = 5;
            public bool SendChatMessageIfNoPermission = true;
            public Dictionary<int, NotificationType> NotificationTypes { get; set; }

            public VersionNumber Version { get; set; }
        }

        private class NotificationType
        {
            public bool Enabled = true;
            public string BackgroundColor = "0.1 0.1 0.1 0.9";
            public string BorderColor = "0.4 0.6 1 1";
            public bool UseGradient = true;
            public string GradientColor = "0.4 0.6 1 0.35";
            public string IconText = "i";
            public string IconColor = "0.4 0.6 1 1";
            public string TitleKey = "Notification";
            public float FadeIn = 0.2f;
            public float FadeOut = 0.5f;
            public string SoundEffect = "assets/bundled/prefabs/fx/notice/item.select.fx.prefab";
            public TextSettings IconSettings = new TextSettings
            {
                AnchorMin = "0 0.5",
                AnchorMax = "0 0.5",
                OffsetMin = "10 -15",
                OffsetMax = "40 15",
                FontSize = 14,
                IsBold = true,
                Align = TextAnchor.MiddleCenter,
                Color = "1 1 1 1"
            };
            public TextSettings TitleSettings = new TextSettings
            {
                AnchorMin = "0 0.5",
                AnchorMax = "1 1",
                OffsetMin = "45 0",
                OffsetMax = "-10 0",
                FontSize = 14,
                IsBold = true,
                Align = TextAnchor.LowerLeft,
                Color = "1 1 1 0.8"
            };
            public TextSettings MessageSettings = new TextSettings
            {
                AnchorMin = "0 0",
                AnchorMax = "1 0.5",
                OffsetMin = "45 5",
                OffsetMax = "-10 0",
                FontSize = 12,
                IsBold = false,
                Align = TextAnchor.UpperLeft,
                Color = "1 1 1 1"
            };
            public bool UseCustomDuration = false;
            public float CustomDuration = 0f;
            public bool UseCustomWidth = false;
            public float CustomWidth = 0f;
            public bool UseCustomHeight = false;
            public float CustomHeight = 0f;
            public bool UseClickCommand = false;
            public string ClickCommand = "";
            public bool CloseAfterCommand = false;
        }

        private class TextSettings
        {
            public string AnchorMin;
            public string AnchorMax;
            public string OffsetMin;
            public string OffsetMax;
            public int FontSize;
            public bool IsBold;
            public TextAnchor Align;
            public string Color;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            
            try
            {
                _config = Config.ReadObject<ConfigData>();
                
                if (_config == null)
                {
                    LoadDefaultConfig();
                }
                else if (_config.Version < Version)
                {
                    UpdateConfig();
                }
            }
            catch
            {
                PrintError("Fehler beim Laden der Konfiguration! Lade Standardkonfiguration...");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Erstelle eine neue Konfiguration...");
            
            _config = new ConfigData
            {
                NotificationTypes = new Dictionary<int, NotificationType>
                {
                    [0] = new NotificationType 
                    {
                        TitleKey = "Notification",
                        BackgroundColor = "0.1 0.1 0.1 0.9",
                        BorderColor = "0.4 0.6 1 1",
                        IconColor = "0.4 0.6 1 1",
                        IconText = "i"
                    },
                    [1] = new NotificationType 
                    {
                        TitleKey = "Error",
                        BackgroundColor = "0.1 0.1 0.1 0.9", 
                        BorderColor = "0.9 0.3 0.3 1",
                        IconColor = "0.9 0.3 0.3 1",
                        IconText = "!"
                    },
                    [2] = new NotificationType 
                    {
                        TitleKey = "Success",
                        BackgroundColor = "0.1 0.1 0.1 0.9",
                        BorderColor = "0.3 0.9 0.3 1",
                        IconColor = "0.3 0.9 0.3 1",
                        IconText = "✓"
                    },
                    [3] = new NotificationType 
                    {
                        TitleKey = "Warning",
                        BackgroundColor = "0.1 0.1 0.1 0.9",
                        BorderColor = "0.9 0.7 0.2 1", 
                        IconColor = "0.9 0.7 0.2 1",
                        IconText = "⚠"
                    },
                    [4] = new NotificationType 
                    {
                        TitleKey = "Event",
                        BackgroundColor = "0.1 0.1 0.1 0.9",
                        BorderColor = "0.6 0.2 0.8 1", // Lila Rahmen
                        IconColor = "0.6 0.2 0.8 1",   // Lila Icon
                        IconText = "★",                // Stern-Symbol für Events
                        UseCustomHeight = true,
                        CustomHeight = 55,             // Etwas höher für bessere Sichtbarkeit
                        UseCustomWidth = true,
                        CustomWidth = 280,             // Etwas breiter für Events
                        FadeIn = 0.3f,                 // Langsameres Einblenden für Aufmerksamkeit
                        FadeOut = 0.8f
                    }
                }
            };
        }
        
        private void UpdateConfig()
        {
            PrintWarning("Aktualisiere Konfiguration auf Version " + Version.ToString());
            
            // Hier Code für future Updates einfügen
            
            _config.Version = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            _instance = this;
            
            // Berechtigungen registrieren
            permission.RegisterPermission(PermSeeNotify, this);
            permission.RegisterPermission(PermNotify, this);
            permission.RegisterPermission(PermPlayerNotify, this);
            permission.RegisterPermission(PermAllPlayersNotify, this);
            
            // Befehle registrieren
            AddCovalenceCommand("mynotify.show", nameof(CmdShowNotify));
            AddCovalenceCommand("mynotify.player", nameof(CmdShowPlayerNotify));
            AddCovalenceCommand("mynotify.allplayers", nameof(CmdShowAllPlayerNotify));
            
            // Kompatibilitätsbefehle für Plugins, die das Original-Notify nutzen
            AddCovalenceCommand("notify.show", nameof(CmdShowNotify));
            AddCovalenceCommand("notify.player", nameof(CmdShowPlayerNotify));
            AddCovalenceCommand("notify.allplayers", nameof(CmdShowAllPlayerNotify));
            
            // Optional: Bilder laden, wenn ImageLibrary installiert ist
            if (ImageLibrary != null && ImageLibrary.IsLoaded)
            {
                // Hier Bilder laden
            }
        }

        private void Unload()
        {
            // Alle Benachrichtigungen beenden
            foreach (var manager in _playerNotifications.Values)
            {
                manager?.Destroy();
            }
            
            // UI für alle Spieler zerstören
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, LAYER_NAME);
            }
            
            _instance = null;
        }

        #endregion

        #region Befehle

        private void CmdShowNotify(IPlayer player, string command, string[] args)
        {
            // Überprüfen, ob der Spieler die Erlaubnis hat
            if (!player.IsServer && !player.HasPermission(PermNotify))
            {
                player.Reply(GetMsg("NoPermission", player.Id));
                return;
            }

            BasePlayer basePlayer = player.Object as BasePlayer;
            if (basePlayer == null) return;

            // Syntax: /mynotify.show [type] [message]
            if (args.Length < 2 || !int.TryParse(args[0], out int type))
            {
                player.Reply(string.Format(GetMsg("SyntaxNotify", player.Id), command));
                return;
            }

            string message = string.Join(" ", args.Skip(1));
            if (string.IsNullOrEmpty(message)) return;

            SendNotify(basePlayer, type, message);
        }

        private void CmdShowPlayerNotify(IPlayer player, string command, string[] args)
        {
            // Überprüfen, ob der Spieler die Erlaubnis hat
            if (!player.IsServer && !player.HasPermission(PermPlayerNotify))
            {
                player.Reply(GetMsg("NoPermission", player.Id));
                return;
            }

            // Syntax: /mynotify.player [playerId] [type] [message]
            if (args.Length < 3 || !int.TryParse(args[1], out int type))
            {
                player.Reply(string.Format(GetMsg("SyntaxPlayerNotify", player.Id), command));
                return;
            }

            // Spieler finden
            IPlayer target = covalence.Players.FindPlayer(args[0]);
            if (target == null || !(target.Object is BasePlayer))
            {
                player.Reply(string.Format(GetMsg("PlayerNotFound", player.Id), args[0]));
                return;
            }

            string message = string.Join(" ", args.Skip(2));
            if (string.IsNullOrEmpty(message)) return;

            SendNotify(target.Object as BasePlayer, type, message);
        }

        private void CmdShowAllPlayerNotify(IPlayer player, string command, string[] args)
        {
            // Überprüfen, ob der Spieler die Erlaubnis hat
            if (!player.IsServer && !player.HasPermission(PermAllPlayersNotify))
            {
                player.Reply(GetMsg("NoPermission", player.Id));
                return;
            }

            // Syntax: /mynotify.allplayers [type] [message]
            if (args.Length < 2 || !int.TryParse(args[0], out int type))
            {
                player.Reply(string.Format(GetMsg("SyntaxAllPlayerNotify", player.Id), command));
                return;
            }

            string message = string.Join(" ", args.Skip(1));
            if (string.IsNullOrEmpty(message)) return;

            SendNotifyToAllPlayers(type, message);
        }

        #endregion
        
        #region API Methoden - Diese bieten die gleiche Schnittstelle wie das Original-Notify

        // Haupt-API-Methode, die von anderen Plugins aufgerufen wird
        private void SendNotify(BasePlayer player, int type, string message)
        {
            if (player == null) return;
            
            if (!permission.UserHasPermission(player.UserIDString, PermSeeNotify))
            {
                // Wenn der Spieler keine Erlaubnis hat, Benachrichtigungen zu sehen
                if (_config.SendChatMessageIfNoPermission)
                    player.ChatMessage(message);
                return;
            }
            
            // Überprüfen, ob der Benachrichtigungstyp existiert und aktiviert ist
            if (!_config.NotificationTypes.TryGetValue(type, out NotificationType notifyType) || !notifyType.Enabled)
            {
                // Fallback auf Standard-Typ 0
                if (!_config.NotificationTypes.TryGetValue(0, out notifyType) || !notifyType.Enabled)
                    return;
            }
            
            // Benachrichtigungsmanager für den Spieler erstellen oder abrufen
            var notificationManager = GetNotificationManager(player);
            if (notificationManager == null) return;
            
            // Neue Benachrichtigung erstellen
            var notification = new NotificationData
            {
                Type = type,
                Message = message,
                CreationTime = Time.realtimeSinceStartup
            };
            
            // Benachrichtigung zur Warteschlange hinzufügen
            notificationManager.AddNotification(notification);
            
            // Soundeffekt abspielen, wenn vorhanden
            if (!string.IsNullOrEmpty(notifyType.SoundEffect))
            {
                PlayEffect(player, notifyType.SoundEffect);
            }
        }
        
        // API-Methode für andere Plugins (Parameter: userId als string)
        private void SendNotify(string userId, int type, string message)
        {
            if (string.IsNullOrEmpty(userId)) return;
            
            ulong steamId;
            if (ulong.TryParse(userId, out steamId))
            {
                SendNotify(BasePlayer.FindByID(steamId), type, message);
            }
        }
        
        // API-Methode für andere Plugins (Parameter: userId als ulong)
        private void SendNotify(ulong userId, int type, string message)
        {
            SendNotify(BasePlayer.FindByID(userId), type, message);
        }
        
        // API-Methode, um eine Benachrichtigung an alle Spieler zu senden
        private void SendNotifyToAllPlayers(int type, string message)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                SendNotify(player, type, message);
            }
        }

        #endregion

        #region Hilfsmethoden

        private NotificationManager GetNotificationManager(BasePlayer player)
        {
            if (player == null) return null;
            
            NotificationManager manager;
            if (_playerNotifications.TryGetValue(player.userID, out manager))
                return manager;
            
            manager = new NotificationManager(player);
            _playerNotifications[player.userID] = manager;
            return manager;
        }
        
        private void PlayEffect(BasePlayer player, string effect)
        {
            if (player == null || string.IsNullOrEmpty(effect)) return;
            
            Effect.server.Run(effect, player.transform.position);
        }

        #endregion

        #region NotificationManager Klasse

        private class NotificationManager
        {
            private readonly BasePlayer _player;
            private readonly List<NotificationData> _notifications = new List<NotificationData>();
            private readonly HashSet<string> _activeNotificationIds = new HashSet<string>();
            private float _lastUpdateTime;
            
            public NotificationManager(BasePlayer player)
            {
                _player = player;
                _lastUpdateTime = Time.realtimeSinceStartup;
                
                // UI-Layer initialisieren
                CreateMainLayer();
                
                // Timer starten
                _instance.timer.Every(0.5f, UpdateNotifications);
            }
            
            public void AddNotification(NotificationData notification)
            {
                if (_notifications.Count >= _instance._config.MaxNotificationsOnScreen)
                {
                    // Älteste Benachrichtigung entfernen, wenn Maximum erreicht ist
                    if (_notifications.Count > 0)
                    {
                        RemoveNotification(0);
                    }
                }
                
                _notifications.Add(notification);
                UpdateUI();
            }
            
            public void RemoveNotification(int index)
            {
                if (index < 0 || index >= _notifications.Count) return;
                
                var notification = _notifications[index];
                _notifications.RemoveAt(index);
                
                if (_activeNotificationIds.Contains(notification.Id))
                {
                    CuiHelper.DestroyUi(_player, LAYER_NAME + "." + notification.Id);
                    _activeNotificationIds.Remove(notification.Id);
                }
                
                UpdateUI();
            }
            
            private void UpdateNotifications()
            {
                if (_player == null || !_player.IsConnected)
                {
                    Destroy();
                    return;
                }
                
                float currentTime = Time.realtimeSinceStartup;
                float deltaTime = currentTime - _lastUpdateTime;
                _lastUpdateTime = currentTime;
                
                // Überprüfen, ob Benachrichtigungen abgelaufen sind
                for (int i = _notifications.Count - 1; i >= 0; i--)
                {
                    var notification = _notifications[i];
                    float duration = GetDuration(notification.Type);
                    
                    if (currentTime - notification.CreationTime >= duration)
                    {
                        RemoveNotification(i);
                    }
                }
                
                // UI aktualisieren, falls erforderlich
                if (_notifications.Count > 0)
                {
                    UpdateUI();
                }
            }
            
            private float GetDuration(int type)
            {
                if (_instance._config.NotificationTypes.TryGetValue(type, out NotificationType notifyType))
                {
                    if (notifyType.UseCustomDuration)
                        return notifyType.CustomDuration;
                }
                
                return _instance._config.DefaultDuration;
            }
            
            private void CreateMainLayer()
            {
                var container = new CuiElementContainer();
                
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image = { Color = "0 0 0 0" }
                }, _instance._config.DisplayType, LAYER_NAME);
                
                CuiHelper.AddUi(_player, container);
            }
            
            private void UpdateUI()
            {
                if (_player == null || !_player.IsConnected) return;
                
                // Aktuelle aktive Benachrichtigungen entfernen
                foreach (var id in _activeNotificationIds)
                {
                    CuiHelper.DestroyUi(_player, LAYER_NAME + "." + id);
                }
                _activeNotificationIds.Clear();
                
                // Neue UI erstellen
                var container = new CuiElementContainer();
                float yPosition = _instance._config.YStartPosition;
                
                for (int i = 0; i < _notifications.Count && i < _instance._config.MaxNotificationsOnScreen; i++)
                {
                    var notification = _notifications[i];
                    float height = CreateNotificationUI(container, notification, yPosition);
                    _activeNotificationIds.Add(notification.Id);
                    
                    yPosition -= (height + _instance._config.YMargin);
                }
                
                CuiHelper.AddUi(_player, container);
            }
            
            private float CreateNotificationUI(CuiElementContainer container, NotificationData notification, float yPosition)
            {
                if (!_instance._config.NotificationTypes.TryGetValue(notification.Type, out NotificationType notifyType))
                {
                    // Fallback auf Standardtyp
                    if (!_instance._config.NotificationTypes.TryGetValue(0, out notifyType))
                        return 0f;
                }
                
                float width = notifyType.UseCustomWidth ? notifyType.CustomWidth : _instance._config.Width;
                float height = notifyType.UseCustomHeight ? notifyType.CustomHeight : _instance._config.Height;
                
                // Hintergrund
                string panelName = LAYER_NAME + "." + notification.Id;
                string anchorSide = _instance._config.ShowAtTopRight ? "1 1" : "0 1";
                string offsetMin = _instance._config.ShowAtTopRight 
                    ? $"{-width - _instance._config.XMargin} {yPosition - height}"
                    : $"{_instance._config.XMargin} {yPosition - height}";
                string offsetMax = _instance._config.ShowAtTopRight
                    ? $"{-_instance._config.XMargin} {yPosition}"
                    : $"{_instance._config.XMargin + width} {yPosition}";
                
                container.Add(new CuiPanel
                {
                    RectTransform = 
                    {
                        AnchorMin = anchorSide,
                        AnchorMax = anchorSide,
                        OffsetMin = offsetMin,
                        OffsetMax = offsetMax
                    },
                    Image = 
                    { 
                        Color = notifyType.BackgroundColor,
                        FadeIn = notifyType.FadeIn
                    },
                    FadeOut = notifyType.FadeOut
                }, LAYER_NAME, panelName);
                
                // Rahmen oben
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0.95", AnchorMax = "1 1" },
                    Image = { Color = notifyType.BorderColor }
                }, panelName);
                
                // Rahmen unten
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.05" },
                    Image = { Color = notifyType.BorderColor }
                }, panelName);
                
                // Rahmen links
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.01 1" },
                    Image = { Color = notifyType.BorderColor }
                }, panelName);
                
                // Rahmen rechts
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.99 0", AnchorMax = "1 1" },
                    Image = { Color = notifyType.BorderColor }
                }, panelName);
                
                // Gradient (optional)
                if (notifyType.UseGradient)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Image = 
                        { 
                            Color = notifyType.GradientColor,
                            FadeIn = notifyType.FadeIn
                        },
                        FadeOut = notifyType.FadeOut
                    }, panelName, panelName + ".Gradient");
                }
                
                // Icon
                container.Add(new CuiPanel
                {
                    RectTransform = 
                    {
                        AnchorMin = notifyType.IconSettings.AnchorMin,
                        AnchorMax = notifyType.IconSettings.AnchorMax,
                        OffsetMin = notifyType.IconSettings.OffsetMin,
                        OffsetMax = notifyType.IconSettings.OffsetMax
                    },
                    Image = 
                    { 
                        Color = notifyType.IconColor,
                        FadeIn = notifyType.FadeIn
                    },
                    FadeOut = notifyType.FadeOut
                }, panelName, panelName + ".Icon");
                
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text = 
                    {
                        Text = notifyType.IconText,
                        Font = notifyType.IconSettings.IsBold ? "robotocondensed-bold.ttf" : "robotocondensed-regular.ttf",
                        FontSize = notifyType.IconSettings.FontSize,
                        Align = notifyType.IconSettings.Align,
                        Color = notifyType.IconSettings.Color,
                        FadeIn = notifyType.FadeIn
                    },
                    FadeOut = notifyType.FadeOut
                }, panelName + ".Icon");
                
                // Titel
                container.Add(new CuiLabel
                {
                    RectTransform = 
                    {
                        AnchorMin = notifyType.TitleSettings.AnchorMin,
                        AnchorMax = notifyType.TitleSettings.AnchorMax,
                        OffsetMin = notifyType.TitleSettings.OffsetMin,
                        OffsetMax = notifyType.TitleSettings.OffsetMax
                    },
                    Text = 
                    {
                        Text = _instance.GetMsg(notifyType.TitleKey, _player.UserIDString),
                        Font = notifyType.TitleSettings.IsBold ? "robotocondensed-bold.ttf" : "robotocondensed-regular.ttf",
                        FontSize = notifyType.TitleSettings.FontSize,
                        Align = notifyType.TitleSettings.Align,
                        Color = notifyType.TitleSettings.Color,
                        FadeIn = notifyType.FadeIn
                    },
                    FadeOut = notifyType.FadeOut
                }, panelName);
                
                // Nachricht
                container.Add(new CuiLabel
                {
                    RectTransform = 
                    {
                        AnchorMin = notifyType.MessageSettings.AnchorMin,
                        AnchorMax = notifyType.MessageSettings.AnchorMax,
                        OffsetMin = notifyType.MessageSettings.OffsetMin,
                        OffsetMax = notifyType.MessageSettings.OffsetMax
                    },
                    Text = 
                    {
                        Text = notification.Message,
                        Font = notifyType.MessageSettings.IsBold ? "robotocondensed-bold.ttf" : "robotocondensed-regular.ttf",
                        FontSize = notifyType.MessageSettings.FontSize,
                        Align = notifyType.MessageSettings.Align,
                        Color = notifyType.MessageSettings.Color,
                        FadeIn = notifyType.FadeIn
                    },
                    FadeOut = notifyType.FadeOut
                }, panelName);
                
                // Klickbaren Button hinzufügen, wenn erforderlich
                if (notifyType.UseClickCommand && !string.IsNullOrEmpty(notifyType.ClickCommand))
                {
                    var button = new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Button = { Color = "0 0 0 0", Command = notifyType.ClickCommand },
                        Text = { Text = "" }
                    };
                    
                    if (notifyType.CloseAfterCommand)
                    {
                        button.Button.Close = panelName;
                    }
                    
                    container.Add(button, panelName);
                }
                
                return height;
            }
            
            public void Destroy()
            {
                if (_player != null && _player.IsConnected)
                {
                    CuiHelper.DestroyUi(_player, LAYER_NAME);
                }
                
                _instance._playerNotifications.Remove(_player.userID);
                _instance.timer.Once(0.1f, () => {
                    _instance.DestroyNotifier(this);
                });
            }
        }
        
        // Methode zum Entfernen eines NotificationManagers
        private void DestroyNotifier(NotificationManager manager)
        {
            // Nichts zu tun, da der Manager bereits aus dem Dictionary entfernt wurde
        }

        #endregion

        #region Sprachnachrichten

        private Dictionary<string, string> GetDefaultMessages()
        {
            return new Dictionary<string, string>
            {
                ["Notification"] = "Benachrichtigung",
                ["Error"] = "Fehler",
                ["Success"] = "Erfolg",
                ["Warning"] = "Warnung",
                ["Event"] = "Event",
                ["NoPermission"] = "Sie haben keine Berechtigung, diesen Befehl zu verwenden.",
                ["SyntaxNotify"] = "Syntax: /{0} [Typ] [Nachricht]",
                ["SyntaxPlayerNotify"] = "Syntax: /{0} [SpielerID] [Typ] [Nachricht]",
                ["SyntaxAllPlayerNotify"] = "Syntax: /{0} [Typ] [Nachricht]",
                ["PlayerNotFound"] = "Spieler '{0}' wurde nicht gefunden!"
            };
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(GetDefaultMessages(), this);
            
            // Englische Übersetzungen
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Notification"] = "Notification",
                ["Error"] = "Error",
                ["Success"] = "Success",
                ["Warning"] = "Warning",
                ["Event"] = "Event",
                ["NoPermission"] = "You don't have permission to use this command.",
                ["SyntaxNotify"] = "Syntax: /{0} [type] [message]",
                ["SyntaxPlayerNotify"] = "Syntax: /{0} [playerID] [type] [message]",
                ["SyntaxAllPlayerNotify"] = "Syntax: /{0} [type] [message]",
                ["PlayerNotFound"] = "Player '{0}' not found!"
            }, this, "en");
            
            // Russische Übersetzungen hinzufügen (optional)
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Notification"] = "Уведомление",
                ["Error"] = "Ошибка",
                ["Success"] = "Успех",
                ["Warning"] = "Предупреждение",
                ["Event"] = "Событие",
                ["NoPermission"] = "У вас нет разрешения использовать эту команду.",
                ["SyntaxNotify"] = "Синтаксис: /{0} [тип] [сообщение]",
                ["SyntaxPlayerNotify"] = "Синтаксис: /{0} [идентификатор игрока] [тип] [сообщение]",
                ["SyntaxAllPlayerNotify"] = "Синтаксис: /{0} [тип] [сообщение]",
                ["PlayerNotFound"] = "Игрок '{0}' не найден!"
            }, this, "ru");
        }
        
        private string GetMsg(string key, string userId = null)
        {
            return lang.GetMessage(key, this, userId);
        }

        #endregion
    }
}