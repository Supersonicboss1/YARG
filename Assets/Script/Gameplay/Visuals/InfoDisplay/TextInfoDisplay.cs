using DG.Tweening;
using TMPro;
using UnityEngine;
using YARG.Core.Engine;
using YARG.Helpers.Extensions;

namespace YARG.Gameplay.Visuals
{
    public class TextInfoDisplay : BaseInfoDisplay
    {
        private       double _judgementFadeTime    = 0.33;
        private const float  TEXT_SLIDE_DISTANCE   = 0.03f;
        private const float  TIMING_SHIFT_DISTANCE = 0.01f;
        [SerializeField]
        private TextMeshPro _comboCounter;
        [SerializeField]
        private TextMeshPro _judgementText;
        [SerializeField]
        private TextMeshPro _starPowerPercentage;

        private Vector3 _judgementTextStartPos;

        private Vector3 _up;

        public override void Initialize(BaseEngine engine)
        {
            base.Initialize(engine);
            _judgementTextStartPos = _judgementText.transform.localPosition;
            _up = _judgementText.transform.localRotation * Vector3.up;
            _comboCounter.color = Color.gold;
            _judgementFadeTime /= GameManager.SongSpeed;
        }

        protected override void OnNoteHit<TNoteType>(TNoteType note)
        {
            base.OnNoteHit(note);
            _judgementText.transform.localPosition =
                _judgementTextStartPos + _up * (LastHitInfo.PercentDelta * TIMING_SHIFT_DISTANCE);
            _judgementText.text = LastHitInfo.Judgement.LocalizedString();
            _judgementText.color = LastHitInfo.Judgement.GetColor();
        }

        private void Update()
        {
            if (LastHitInfo.Time + _judgementFadeTime > GameManager.VisualTime)
            {
                var progress = DOVirtual.EasedValue(0, 1,
                    (float) ((GameManager.VisualTime - LastHitInfo.Time) / _judgementFadeTime), Ease.OutSine);
                _judgementText.color = _judgementText.color.WithAlpha(1 - progress);
                _judgementText.transform.localPosition = Vector3.Lerp(
                    _judgementTextStartPos + _up * (LastHitInfo.PercentDelta * TIMING_SHIFT_DISTANCE),
                    _judgementTextStartPos +
                    _up * (LastHitInfo.PercentDelta * TIMING_SHIFT_DISTANCE) + Vector3.left * TEXT_SLIDE_DISTANCE,
                    progress);
            }
            else
            {
                _judgementText.color = Color.clear;
            }

            // if (Engine is null)
            // {
            //     return;
            // }
            //
            // float spPercent = (float) Engine.BaseStats.StarPowerTickAmount / Engine.TicksPerFullSpBar;
            // if (!Mathf.Approximately(StarPowerPercent, spPercent))
            // {
            //     StarPowerPercent = spPercent;
            //     _starPowerPercentage.text = StarPowerPercent.ToString("P2");
            //     _starPowerPercentage.color = Engine.CanStarPowerActivate || Engine.BaseStats.IsStarPowerActive ? Color.gold : Color.white;
            // }
        }

        public override void SetCombo(int combo)
        {
            if (combo == Combo)
            {
                return;
            }

            _comboCounter.text = combo.ToString();
            base.SetCombo(combo);
        }

        protected override void SetFc(bool fc)
        {
            if (fc == IsFc)
            {
                return;
            }

            _comboCounter.color = fc ? Color.gold : Color.white;
            base.SetFc(fc);
        }
    }
}