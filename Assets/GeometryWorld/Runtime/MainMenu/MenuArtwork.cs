using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public sealed class MenuArtwork : ScriptableObject
    {
        [Header("Typography and captions")]
        public Font displayFont, labelFont;
        public Sprite focusMarker;
        public string skipIntro = "Skip intro";

        [Header("Islands")]
        public string newGame = "New game";
        public string continueGame = "Continue";
        public string general = "Video";
        public string audio = "Audio";
        public string noJourney = "No save yet";
        public string journeyUnavailable = "Could not load the game.";

        [Header("Settings")]
        public string low = "Low";
        public string medium = "Medium";
        public string high = "High";
        public string fullscreenOn = "Fullscreen on";
        public string fullscreenOff = "Fullscreen off";
        public string fullscreenEditor = "Fullscreen is available in the player";
        public string fullscreenUnsupported = "Fullscreen is unavailable in this browser";
        public string fullscreenFailed = "The display did not change fullscreen mode";
        public string master = "Master";
        public string music = "Music";
        public string effects = "Sound effects";
        public string back = "Back";

        [Header("Reset confirmation")]
        public string confirmTitle = "Start a new game?";
        public string confirmMessage = "This replaces your current save.";
        public string keepJourney = "Keep my save";
        public string startOver = "New game";
    }
}
