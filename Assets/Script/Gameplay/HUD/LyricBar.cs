using Cysharp.Text;
using TMPro;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Settings;

namespace YARG.Gameplay.HUD
{
    public enum LyricDisplayMode
    {
        Disabled,
        Normal,
        Transparent,
        NoBackground,
    }

    public class LyricBar : GameplayBehaviour
    {
        [SerializeField]
        private GameObject _normalBackground;
        [SerializeField]
        private GameObject _transparentBackground;
        [SerializeField]
        private LyricBarPhrase _lyricA;
        [SerializeField]
        private LyricBarPhrase _lyricB;
        // Phrases will alternate between these two objects

        private LyricsTrack _lyrics;
        private int _currentPhraseIndex = 0;
        private double _upcomingLyricsThreshold;

        protected override void GameplayAwake()
        {
            var lyricSetting = SettingsManager.Settings.LyricDisplay.Value;

            if (GameManager.IsPractice || lyricSetting == LyricDisplayMode.Disabled)
            {
                gameObject.SetActive(false);
                return;
            }

            // Set the lyric background
            switch (lyricSetting)
            {
                case LyricDisplayMode.Normal:
                    _normalBackground.SetActive(true);
                    _transparentBackground.SetActive(false);
                    break;
                case LyricDisplayMode.Transparent:
                    _normalBackground.SetActive(false);
                    _transparentBackground.SetActive(true);
                    break;
                case LyricDisplayMode.NoBackground:
                    _normalBackground.SetActive(false);
                    _transparentBackground.SetActive(false);
                    break;
            }
            // How much time before a phrase starts should it be displayed?
            _upcomingLyricsThreshold = SettingsManager.Settings.UpcomingLyricsTime.Value;
        }

        protected override void OnChartLoaded(SongChart chart)
        {
            _lyrics = chart.Lyrics;
            if (_lyrics.Phrases.Count < 1)
            {
                gameObject.SetActive(false);
                return;
            }
            _lyricA.gameObject.SetActive(true);
            _lyricB.gameObject.SetActive(true);
        }

        private void Update()
        {
            const double PHRASE_DISTANCE_THRESHOLD = 1.0;


            var phrases = _lyrics.Phrases;
            float timeToNextPhrase = (float) (phrases[_currentPhraseIndex].Time - GameManager.SongTime);


            var activePhrase = _currentPhraseIndex % 2 == 0 ? _lyricA : _lyricB;
            var inactivePhrase = _currentPhraseIndex % 2 == 0 ? _lyricB : _lyricA;
            /*
            Conditions for lyrics displaying:
            1. If there is no lyric in the main bar, display the next lyric in the main bar if it is within the _upcomingLyricsThreshold
            2. If the current lyric is in the main bar, display the next lyric in the upcoming bar if the end of the current lyric - the start of the next lyric is greater than the PHRASE_DISTANCE_THRESHOLD
            3. Once a lyric finishes, it automatically moves out of the main bar (handled by Lyric.cs)
            4. If a lyric is in the upcoming bar, it will move to the main bar when the other moves out.
            */
            if (!activePhrase.isInMainBar && !inactivePhrase.isInMainBar && timeToNextPhrase <= _upcomingLyricsThreshold)
            {
                activePhrase.SetPhrase(phrases[_currentPhraseIndex]);
                activePhrase.TransitionToMain(activePhrase.DEFAULT_ANIMATION_TIME);
            }
            if (_currentPhraseIndex + 1 >= phrases.Count)
            {
                activePhrase.timeOfNextPhrase = double.MaxValue; // To ensure the last phrase animates out properly
                return;
            }
            if (activePhrase.isInMainBar && !inactivePhrase.isInMainBar && phrases[_currentPhraseIndex + 1].Time - phrases[_currentPhraseIndex].TimeEnd <= _upcomingLyricsThreshold)
            {
                inactivePhrase.SetPhrase(phrases[_currentPhraseIndex + 1]);
            }
            // if there is no lyric in the main bar, display the next lyric in the main bar
            if (!activePhrase.isInMainBar && !inactivePhrase.isInMainBar)
            {
                inactivePhrase.TransitionToMain(activePhrase.DEFAULT_ANIMATION_TIME);
            }
            activePhrase.timeOfNextPhrase = phrases[_currentPhraseIndex + 1].TimeEnd; // This is to handle charts where two phrases end within 0.1 seconds of each other
            // If the current phrase ended AND
            while (_currentPhraseIndex < phrases.Count && phrases[_currentPhraseIndex].TimeEnd <= GameManager.SongTime &&
                // Was the last phrase
                (_currentPhraseIndex + 1 == phrases.Count ||
                 // OR if the next phrase is one second or more away (leading to an empty bar)
                 phrases[_currentPhraseIndex + 1].Time - phrases[_currentPhraseIndex].TimeEnd >= PHRASE_DISTANCE_THRESHOLD ||
                 // OR if the next phrase should be started
                 phrases[_currentPhraseIndex + 1].Time <= GameManager.SongTime))
            {
                _currentPhraseIndex++;
            }

        }

    }
}