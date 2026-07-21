using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Keys;
using YARG.Core.Logging;
using YARG.Localization;

namespace YARG.Gameplay.Visuals
{
    public enum Judgement
    {
        UltraPerfect,
        Perfect,
        Great,
        Good,
        Bad
    }

    public static class JudgementExtensions
    {
        public static Color GetColor(this Judgement judgement)
        {
            return judgement switch
            {
                Judgement.UltraPerfect => Color.white,
                Judgement.Perfect      => Color.cyan,
                Judgement.Great        => Color.green,
                Judgement.Good         => Color.yellow,
                Judgement.Bad          => Color.red,
                _                      => throw new ArgumentOutOfRangeException(nameof(judgement), judgement, null)
            };
        }

        public static string LocalizedString(this Judgement judgement)
        {
            return judgement switch
            {
                Judgement.UltraPerfect => Localize.Key("Perfect"),
                Judgement.Perfect      => Localize.Key("Perfect"),
                Judgement.Great        => Localize.Key("Great"),
                Judgement.Good         => Localize.Key("Good"),
                Judgement.Bad          => Localize.Key("Bad"),
                _                      => throw new ArgumentOutOfRangeException(nameof(judgement), judgement, null)
            };
        }
    }

    public abstract class BaseInfoDisplay : GameplayBehaviour
    {
        protected enum Timing
        {
            Early   = 1,
            Perfect = 0,
            Late    = -1
        }

        protected struct HitInfo
        {
            public readonly Judgement Judgement;
            public readonly double    Time;
            public readonly double    TimeDelta;
            public readonly float     PercentDelta;
            public readonly Timing    Timing;

            public HitInfo(double time, double timeDelta, float percentDelta)
            {
                Time = time;
                TimeDelta = timeDelta;
                PercentDelta = percentDelta;
                Judgement = GetJudgement(percentDelta);
                Timing = timeDelta switch
                {
                    < 0 => Timing.Late,
                    0   => Timing.Perfect,
                    > 0 => Timing.Early,
                    _   => throw new ArgumentOutOfRangeException(nameof(timeDelta), timeDelta, null)
                };
            }
        }

        protected        HitInfo      LastHitInfo;
        protected        float        StarPowerPercent;
        protected        bool         IsFc;
        protected        int          Combo;
        protected        BaseEngine   Engine;
        private readonly List<Action> _unsubscribeActions = new();

        public virtual void Initialize(BaseEngine engine)
        {
            Engine = engine;
            SubscribeToEngineEvents(engine);
            SetFc(true);
        }

        protected override void GameplayDestroy()
        {
            Engine = null;
            foreach (var action in _unsubscribeActions)
            {
                action?.Invoke();
            }

            _unsubscribeActions.Clear();
        }

        protected virtual void OnNoteHit<TNoteType>(TNoteType note) where TNoteType : Note<TNoteType>
        {
            var delta = Engine.CurrentTime - note.Time;
            double percent = delta /
                (delta > 0 ? Engine.CalculateHitWindow().FrontEnd : Engine.CalculateHitWindow().BackEnd);
            LastHitInfo = new HitInfo(note.Time, delta, (float) percent);
        }

        /// <summary>
        /// Get the judgement type based on how far away the note was hit from a perfect hit.
        /// </summary>
        /// <param name="percentageOfHitWindow">As a percentage (0-1), how far away from the centre of the hit window are you, with 0 as a perfect hit.</param>
        /// <returns></returns>
        protected static Judgement GetJudgement(double percentageOfHitWindow)
        {
            return Math.Abs(percentageOfHitWindow) switch
            {
                >= 0.75 => Judgement.Bad,
                >= 0.5  => Judgement.Good,
                >= 0.25 => Judgement.Great,
                0       => Judgement.UltraPerfect,
                _       => Judgement.Perfect
            };
        }

        public virtual void SetCombo(int combo)
        {
            if (combo < Combo)
            {
                SetFc(Engine.BaseStats.IsFullCombo);
            }

            Combo = combo;
        }

        protected virtual void SetFc(bool fc)
        {
            IsFc = fc;
        }

        private void SubscribeToEngineEvents(BaseEngine engine)
        {
            switch (engine)
            {
                case GuitarEngine guitarEngine:
                    GuitarEngine.NoteHitEvent guitarNoteHit =
                        (_, note) => { OnNoteHit(note); };
                    guitarEngine.OnNoteHit += guitarNoteHit;
                    _unsubscribeActions.Add(() => guitarEngine.OnNoteHit -= guitarNoteHit);
                    break;
                case DrumsEngine drumsEngine:
                    DrumsEngine.NoteHitEvent drumsNoteHit = (_, note) => { OnNoteHit(note); };
                    drumsEngine.OnNoteHit += drumsNoteHit;
                    _unsubscribeActions.Add(() => drumsEngine.OnNoteHit -= drumsNoteHit);
                    break;
                case KeysEngine<ProKeysNote> proKeysEngine:
                    KeysEngine<ProKeysNote>.NoteHitEvent proKeysNoteHit = (_, note) => { OnNoteHit(note); };
                    proKeysEngine.OnNoteHit += proKeysNoteHit;
                    _unsubscribeActions.Add(() => proKeysEngine.OnNoteHit -= proKeysNoteHit);
                    break;
                case KeysEngine<GuitarNote> fiveLaneKeysEngine:
                    KeysEngine<GuitarNote>.NoteHitEvent fiveLaneKeysNoteHit =
                        (_, note) => { OnNoteHit(note); };
                    fiveLaneKeysEngine.OnNoteHit += fiveLaneKeysNoteHit;
                    _unsubscribeActions.Add(() => fiveLaneKeysEngine.OnNoteHit -= fiveLaneKeysNoteHit);
                    break;
            }
        }

        public void SetSongTime(double time)
        {
            SetFc(Engine.BaseStats.IsFullCombo);
            LastHitInfo = new HitInfo(double.MinValue, 0, 0);
        }
    }
}