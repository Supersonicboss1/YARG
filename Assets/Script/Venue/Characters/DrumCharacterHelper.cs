using System;
using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Logging;

namespace YARG.Venue.Characters
{
    public class DrumCharacterHelper
    {
        private const double REPEAT_THRESHOLD = 0.125;
        private readonly Random _random = new();

        private enum Hand
        {
            LEFT,
            RIGHT,
        }

        private sealed class PadState
        {
            public Hand    DefaultHand  { get; }
            public Hand?   LastUsedHand { get; private set; }
            public double? LastHitTime  { get; private set; }
            public PadState(Hand defaultHand)
            {
                DefaultHand = defaultHand;
            }
            public void RecordHit(Hand selectedHand, double noteTime)
            {
                LastUsedHand = selectedHand;
                LastHitTime = noteTime;
            }

            public void Reset()
            {
                LastUsedHand = null;
                LastHitTime = null;
            }
        }

        private readonly IReadOnlyDictionary<FourLaneDrumPad, PadState> _handStateByPad
            = new Dictionary<FourLaneDrumPad, PadState>
            {
                { FourLaneDrumPad.YellowCymbal, new PadState(Hand.RIGHT) },
                { FourLaneDrumPad.BlueCymbal, new PadState(Hand.RIGHT) },
                { FourLaneDrumPad.GreenCymbal, new PadState(Hand.RIGHT) },
                { FourLaneDrumPad.GreenDrum, new PadState(Hand.RIGHT) },
                { FourLaneDrumPad.BlueDrum, new PadState(Hand.RIGHT) },
                { FourLaneDrumPad.YellowDrum, new PadState(Hand.RIGHT) },
                { FourLaneDrumPad.RedDrum, new PadState(Hand.LEFT) },
            };

        private readonly double?[] _lastHitTimeByHand = new double?[2];
        public List<AnimationEvent> GetDrumAnimations(List<DrumNote> drumNotes)
        {
            foreach (var state in _handStateByPad.Values)
            {
                state.Reset();
            }
            Array.Clear(_lastHitTimeByHand, 0, _lastHitTimeByHand.Length);

            var drumAnimationEvents = new List<AnimationEvent>();
            foreach (var parent in drumNotes)
            {
                var animations = GetAnimationsForParentNote(parent);
                foreach (var animation in animations)
                {
                    drumAnimationEvents.Add(
                        new AnimationEvent(animation, parent.Time, parent.TimeLength, parent.Tick, parent.TickLength)
                    );
                }
            }

            return drumAnimationEvents;
        }

        private List<AnimationEvent.AnimationType> GetAnimationsForParentNote(DrumNote parentNote)
        {
            var animations = new List<AnimationEvent.AnimationType>();
            var padHands = new List<(DrumNote Note, FourLaneDrumPad Pad, Hand Hand)>();

            foreach (var note in parentNote.AllNotes)
            {
                var pad = (FourLaneDrumPad) note.Pad;
                if (pad == FourLaneDrumPad.Kick)
                {
                    // Kick doesn't need hand tracking, add animation directly
                    // TODO: may need to consider this further if we ever want to support double-kick on the drummer
                    animations.Add(AnimationEvent.AnimationType.Kick);
                    continue;
                }
                var hand = GetHandForPad(pad, note.Time);
                padHands.Add((note, pad, hand));
            }

            ResolveSimultaneousHandHits(padHands);

            // Alternate consecutive snare hits, even when they are spaced too far apart
            // TODO: should probably do something about fast snare rolls to make them double-stroke
            var isSoloSnare = padHands.Count == 1 && padHands[0].Pad == FourLaneDrumPad.RedDrum;
            var previousWasSnare = PreviousWasSnareHit(parentNote.PreviousNote);
            if (isSoloSnare && previousWasSnare)
            {
                var lastHand = _handStateByPad[FourLaneDrumPad.RedDrum].LastUsedHand;
                if (lastHand.HasValue)
                {
                    padHands[0] = (padHands[0].Note, padHands[0].Pad, SwitchHand(lastHand.Value));
                }
            }

            // Recorded for future hand decision consistency
            foreach (var (note, pad, hand) in padHands)
            {
                _handStateByPad[pad].RecordHit(hand, note.Time);
                _lastHitTimeByHand[(int)hand] = note.Time;
                animations.Add(GetAnimationType(pad, hand, note.Type));
            }

            return animations;
        }

        private Hand GetHandForPad(FourLaneDrumPad pad, double noteTime)
        {
            var state = _handStateByPad.GetValueOrDefault(pad);
            if (state is null)
            {
                YargLogger.LogFormatWarning("Unknown drum pad {0}, defaulting to right", pad);
                return Hand.RIGHT;
            }

            // Blue cymbal should stay right hand to prevent excessive twisting
            if (IsBlueCymbal(pad))
            {
                return state.DefaultHand;
            }

            var isRepeatedNote = state.LastHitTime.HasValue
                && noteTime - state.LastHitTime.Value <= REPEAT_THRESHOLD;
            var hand = isRepeatedNote && state.LastUsedHand.HasValue
                ? SwitchHand(state.LastUsedHand.Value)
                : state.DefaultHand;

            // During quick fills, alternate across different pads too,
            // but do not switch onto a hand that is already busy
            if (_lastHitTimeByHand[(int)hand] is { } lastHitTime
                && noteTime >= lastHitTime
                && noteTime - lastHitTime <= REPEAT_THRESHOLD)
            {
                var alternateHand = SwitchHand(hand);
                var alternateHitTime = _lastHitTimeByHand[(int)alternateHand];
                if (!alternateHitTime.HasValue || noteTime < alternateHitTime.Value
                    || noteTime - alternateHitTime.Value > REPEAT_THRESHOLD)
                {
                    hand = alternateHand;
                }
            }

            return hand;
        }

        private static void ResolveSimultaneousHandHits(List<(DrumNote Note, FourLaneDrumPad Pad, Hand Hand)> padHands)
        {
            for (int i = 1; i < padHands.Count; i++)
            {
                int duplicateIndex = -1;
                for (int j = 0; j < i; j++)
                {
                    if (padHands[j].Hand == padHands[i].Hand)
                    {
                        duplicateIndex = j;
                        break;
                    }
                }

                if (duplicateIndex < 0)
                {
                    continue;
                }

                // Keep the blue cymbal on the right where possible. Otherwise
                // preserve the later hit and switch the earlier default-hand hit.
                int indexToSwitch = IsBlueCymbal(padHands[duplicateIndex].Pad) ? i : duplicateIndex;
                var hitToSwitch = padHands[indexToSwitch];
                var otherHand = SwitchHand(hitToSwitch.Hand);
                bool otherHandAlreadyUsed = false;
                for (int j = 0; j < i; j++)
                {
                    if (j != indexToSwitch && padHands[j].Hand == otherHand)
                    {
                        otherHandAlreadyUsed = true;
                        break;
                    }
                }

                if (!otherHandAlreadyUsed)
                {
                    padHands[indexToSwitch] = (hitToSwitch.Note, hitToSwitch.Pad, otherHand);
                }
                else
                {
                    // Both hands are already hitting something, this extra pad cannot be struck
                    padHands.RemoveAt(i);
                    i--;
                }
            }
        }

        private static bool PreviousWasSnareHit(DrumNote previousNote)
        {
            if (previousNote is null)
            {
                return false;
            }

            foreach (var note in previousNote.AllNotes)
            {
                var pad = (FourLaneDrumPad) note.Pad;
                if (pad != FourLaneDrumPad.RedDrum && pad != FourLaneDrumPad.Kick)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsBlueCymbal(FourLaneDrumPad pad)
        {
            return pad == FourLaneDrumPad.BlueCymbal;
        }

        private static Hand SwitchHand(Hand hand)
        {
            return hand == Hand.LEFT ? Hand.RIGHT : Hand.LEFT;
        }

        private AnimationEvent.AnimationType GetRandomCrashAnimation(Hand hand, bool isSoft)
        {
            bool useCrash2 = _random.Next(2) == 0;
            return (useCrash2, hand, isSoft) switch
            {
                (false, Hand.LEFT, false)  => AnimationEvent.AnimationType.Crash1LhHard,
                (false, Hand.RIGHT, false) => AnimationEvent.AnimationType.Crash1RhHard,
                (true,  Hand.LEFT, false)  => AnimationEvent.AnimationType.Crash2LhHard,
                (true,  Hand.RIGHT, false) => AnimationEvent.AnimationType.Crash2RhHard,
                (false, Hand.LEFT, true)   => AnimationEvent.AnimationType.Crash1LhSoft,
                (false, Hand.RIGHT, true)  => AnimationEvent.AnimationType.Crash1RhSoft,
                (true,  Hand.LEFT, true)   => AnimationEvent.AnimationType.Crash2LhSoft,
                (true,  Hand.RIGHT, true)  => AnimationEvent.AnimationType.Crash2RhSoft,
                _ => AnimationEvent.AnimationType.Crash1LhHard,
            };
        }

        private AnimationEvent.AnimationType GetAnimationType(FourLaneDrumPad pad, Hand hand, DrumNoteType noteType)
        {
            bool isSoft = noteType == DrumNoteType.Ghost;
            return pad switch
            {
                FourLaneDrumPad.RedDrum => hand == Hand.LEFT
                    ? (isSoft ? AnimationEvent.AnimationType.SnareLhSoft : AnimationEvent.AnimationType.SnareLhHard)
                    : (isSoft ? AnimationEvent.AnimationType.SnareRhSoft : AnimationEvent.AnimationType.SnareRhHard),

                FourLaneDrumPad.YellowCymbal => hand == Hand.LEFT
                    ? AnimationEvent.AnimationType.HihatLeftHand
                    : AnimationEvent.AnimationType.HihatRightHand,

                FourLaneDrumPad.BlueCymbal => hand == Hand.LEFT
                    ? AnimationEvent.AnimationType.RideLh
                    : AnimationEvent.AnimationType.RideRh,

                FourLaneDrumPad.GreenCymbal => GetRandomCrashAnimation(hand, isSoft),

                FourLaneDrumPad.YellowDrum => hand == Hand.LEFT
                    ? AnimationEvent.AnimationType.Tom1LeftHand
                    : AnimationEvent.AnimationType.Tom1RightHand,

                FourLaneDrumPad.BlueDrum => hand == Hand.LEFT
                    ? AnimationEvent.AnimationType.Tom2LeftHand
                    : AnimationEvent.AnimationType.Tom2RightHand,

                FourLaneDrumPad.GreenDrum => hand == Hand.LEFT
                    ? AnimationEvent.AnimationType.FloorTomLeftHand
                    : AnimationEvent.AnimationType.FloorTomRightHand,

                FourLaneDrumPad.Kick => AnimationEvent.AnimationType.Kick,

                _ => throw new ArgumentOutOfRangeException(nameof(pad), pad, "Unsupported drum pad for animation mapping."),
            };
        }
    }
}
