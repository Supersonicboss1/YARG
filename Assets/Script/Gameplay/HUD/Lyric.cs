using Cysharp.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Logging;

namespace YARG.Gameplay.HUD
{

    public class LyricBarPhrase : GameplayBehaviour
    {

        [SerializeField]
        private TextMeshProUGUI _lyricText;

        private LyricsPhrase _phrase;
        private int _currentLyricIndex = 0;

        public double timeOfNextPhrase; // This is used in case the next phrase is too close to the current one, so we need to animate it faster

        public bool isInMainBar = false;

        private Vector2 INITIAL_POSITION = new(0, -60);
        private Vector2 UPCOMING_POSITION = new(0, -53.7f);
        private Vector2 MAIN_POSITION = new(0, 0);
        private Vector2 END_POSITION = new(0, 30);
        private float UPCOMING_ALPHA = 0.36f;
        public float DEFAULT_ANIMATION_TIME = 0.1f;
        private int UPCOMING_FONT_SIZE = 28;
        private int MAIN_FONT_SIZE = 36;


        private void Reset() {
            _lyricText.rectTransform.anchoredPosition = INITIAL_POSITION;
            _lyricText.alpha = 0;
            _lyricText.fontSize = UPCOMING_FONT_SIZE;
            _lyricText.SetText(string.Empty);
        }
        public void SetPhrase(LyricsPhrase phrase)
        {
            if (phrase == null || phrase == _phrase || _lyricText.rectTransform.anchoredPosition != INITIAL_POSITION)
                return;
            _phrase = phrase;
            Reset();
            isInMainBar = false;
            _currentLyricIndex = 0;
            // it starts at 0, -60, 0, then needs to tween to 0, -53.7f, 0, and alpha 1, over 0.1sec, or the time until the next lyric, whichever is shorter
            _lyricText.SetText(BuildPhraseString(_phrase));
            var timeForAnimate = Mathf.Min(DEFAULT_ANIMATION_TIME, (float) (_phrase.Time - GameManager.SongTime));
            // if the time for animation is less than 0.1, we need to animate NOW, so place the phrase at -53.7f and immediately animate it to main.
            if (timeForAnimate < DEFAULT_ANIMATION_TIME)
            {
                _lyricText.rectTransform.anchoredPosition = UPCOMING_POSITION;
                _lyricText.alpha = UPCOMING_ALPHA;
                TransitionToMain(timeForAnimate);
                return;
            }
            _lyricText.rectTransform.DOAnchorPosY(UPCOMING_POSITION.y, DEFAULT_ANIMATION_TIME);
            DOTween.To(() => _lyricText.alpha, x => _lyricText.alpha = x, UPCOMING_ALPHA, DEFAULT_ANIMATION_TIME);
        }


        // This is public so that transition to main can be called from the lyric bar
        public void TransitionToMain(float timeForAnimate)
        {
            // If the object is is not in the correct position, or the phrase is null, don't animate, something is wrong
            if (_lyricText.rectTransform.anchoredPosition != UPCOMING_POSITION || _phrase == null)
                return;
            _lyricText.rectTransform.DOAnchorPosY(MAIN_POSITION.y, timeForAnimate);
            DOTween.To(() => _lyricText.fontSize, x => _lyricText.fontSize = x, MAIN_FONT_SIZE, timeForAnimate);
            DOTween.To(() => _lyricText.alpha, x => _lyricText.alpha = x, 1, timeForAnimate);
                isInMainBar = true;

        }
        private void Update()
        {
            // Don't bother updating if the lyric is invalid
            if (_phrase == null)
                return;
            var lyrics = _phrase.Lyrics;
            // if the current time is past the time of the last lyric, animate to 30y, fade out, and move it to -60y
            if (GameManager.SongTime > _phrase.TimeEnd && isInMainBar)
            {
                var timeForAnimate = Mathf.Min(DEFAULT_ANIMATION_TIME, (float) (timeOfNextPhrase - GameManager.SongTime));
                if (timeForAnimate < 0.1)
                {
                    YargLogger.LogDebug("Lyric", "Time for animate is " + timeForAnimate);
                }
                isInMainBar = false;
                if (timeForAnimate <= 0)
                    Reset();
                _lyricText.rectTransform.DOAnchorPosY(END_POSITION.y, timeForAnimate);
                DOTween.To(() => _lyricText.alpha, x => _lyricText.alpha = x, 0, timeForAnimate);
                DOVirtual.DelayedCall(timeForAnimate, () => Reset());
            }
            // Check following lyrics
            int currIndex = _currentLyricIndex;
            while (currIndex < lyrics.Count && lyrics[currIndex].Time <= GameManager.SongTime)
                currIndex++;

            // No update necessary
            if (_currentLyricIndex == currIndex)
                return;

            // Construct lyrics to be displayed
            using var output = ZString.CreateStringBuilder(true);

            // Start highlight
            output.Append("<color=#5CB9FF>");

            int i = 0;
            while (i < currIndex)
            {
                var lyric = lyrics[i++];
                output.Append(lyric.Text);
                if (!lyric.JoinWithNext && i < lyrics.Count)
                    output.Append(' ');
            }

            // End highlight
            output.Append("</color>");

            while (i < lyrics.Count)
            {
                var lyric = lyrics[i++];
                output.Append(lyric.Text);
                if (!lyric.JoinWithNext && i < lyrics.Count)
                    output.Append(' ');
            }

            _currentLyricIndex = currIndex;
            _lyricText.SetText(output);
        }
        private Utf16ValueStringBuilder BuildPhraseString(LyricsPhrase phrase)
        {
            using var output = ZString.CreateStringBuilder();
            if (phrase == null)
                return output;
            int i = 0;
            while (i < phrase.Lyrics.Count)
            {
                var lyric = phrase.Lyrics[i++];
                output.Append(lyric.Text);
                if (!lyric.JoinWithNext && i < phrase.Lyrics.Count)
                    output.Append(' ');
            }
            return output;
        }
    }
}