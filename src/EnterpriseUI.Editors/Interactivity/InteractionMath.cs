namespace EnterpriseUI.Editors.Interactivity;

public enum EasingKind { Linear, EaseIn, EaseOut, EaseInOut, Cubic, Back }

public static class Easing
{
    /// <summary>Maps linear progress 0..1 to eased progress; the result stays within 0..1 except for <see cref="EasingKind.Back"/> which overshoots slightly.</summary>
    public static double Apply(EasingKind kind, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return kind switch
        {
            EasingKind.EaseIn => t * t,
            EasingKind.EaseOut => 1 - (1 - t) * (1 - t),
            EasingKind.EaseInOut => t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2,
            EasingKind.Cubic => t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2,
            EasingKind.Back => 1 + 2.70158 * Math.Pow(t - 1, 3) + 1.70158 * Math.Pow(t - 1, 2),
            _ => t
        };
    }
}

public enum RatingPrecision { Full, Half, Exact }

/// <summary>Maps pointer positions to rating values and rating values to the fill of each symbol.</summary>
public static class RatingMath
{
    /// <summary>Value for a pointer at <paramref name="position"/> inside <paramref name="extent"/> (the width or height covered by all symbols).</summary>
    public static double ValueAt(double position, double extent, int maxValue, RatingPrecision precision, bool reversed = false)
    {
        if (extent <= 0 || maxValue <= 0) return 0;
        var fraction = Math.Clamp(position / extent, 0, 1);
        if (reversed) fraction = 1 - fraction;
        var raw = fraction * maxValue;
        return Snap(raw, maxValue, precision);
    }

    public static double Snap(double value, int maxValue, RatingPrecision precision)
    {
        value = Math.Clamp(value, 0, maxValue);
        return precision switch
        {
            RatingPrecision.Full => Math.Ceiling(value - 1e-9),
            RatingPrecision.Half => Math.Ceiling(value * 2 - 1e-9) / 2,
            _ => Math.Round(value, 2)
        };
    }

    /// <summary>Fraction 0..1 of the symbol at <paramref name="index"/> that is filled for the value.</summary>
    public static double FillOf(int index, double value) => Math.Clamp(value - index, 0, 1);

    public static double Step(double value, int direction, int maxValue, RatingPrecision precision)
    {
        var step = precision == RatingPrecision.Full ? 1.0 : precision == RatingPrecision.Half ? 0.5 : 0.1;
        return Snap(Math.Round(value + direction * step, 2), maxValue, precision);
    }
}

/// <summary>Value scale of a track bar: snapping, keyboard steps and value/position mapping.</summary>
public sealed class TrackScale
{
    public double Minimum { get; set; }
    public double Maximum { get; set; } = 100;
    public double SmallChange { get; set; } = 1;
    public double LargeChange { get; set; } = 10;

    /// <summary>Values snap to multiples of this step from the minimum; 0 means continuous.</summary>
    public double Step { get; set; }

    public double Range => Math.Max(0, Maximum - Minimum);

    public double Snap(double value)
    {
        value = Math.Clamp(value, Minimum, Maximum);
        if (Step > 0) value = Minimum + Math.Round((value - Minimum) / Step) * Step;
        return Math.Clamp(Math.Round(value, 6), Minimum, Maximum);
    }

    public double ToFraction(double value) => Range <= 0 ? 0 : Math.Clamp((value - Minimum) / Range, 0, 1);

    public double FromFraction(double fraction) => Snap(Minimum + Math.Clamp(fraction, 0, 1) * Range);

    public double ToPosition(double value, double start, double length) => start + ToFraction(value) * length;

    public double FromPosition(double position, double start, double length) => length <= 0 ? Minimum : FromFraction((position - start) / length);

    public double Move(double value, double amount) => Snap(value + amount);

    /// <summary>Tick values every <paramref name="tickFrequency"/> units, always including both ends.</summary>
    public IReadOnlyList<double> Ticks(double tickFrequency)
    {
        var result = new List<double>();
        if (tickFrequency <= 0 || Range <= 0) return result;
        var count = (int)Math.Floor(Range / tickFrequency + 1e-9);
        if (count > 2000) return result;
        for (var i = 0; i <= count; i++) result.Add(Math.Round(Minimum + i * tickFrequency, 6));
        if (result[^1] < Maximum) result.Add(Maximum);
        return result;
    }
}

public enum StepStatus { Pending, Active, Completed, Warning, Error, Skipped }

public sealed class StepEntry
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Glyph { get; set; }
    public bool IsOptional { get; set; }
    public StepStatus Status { get; internal set; } = StepStatus.Pending;
    public string? Message { get; set; }
}

public sealed class StepChangingEventArgs : EventArgs
{
    public StepChangingEventArgs(int from, int to) { From = from; To = to; }
    public int From { get; }
    public int To { get; }
    public bool Cancel { get; set; }
    public string? Message { get; set; }
}

/// <summary>Step sequence with navigation rules: stepping forward validates the current step, going back is always allowed.</summary>
public sealed class StepFlow
{
    private int _current;

    public IList<StepEntry> Steps { get; } = new List<StepEntry>();

    /// <summary>When false only steps up to the first incomplete one can be reached by clicking.</summary>
    public bool AllowJumpAhead { get; set; }

    public int Current
    {
        get => _current;
        private set => _current = Steps.Count == 0 ? 0 : Math.Clamp(value, 0, Steps.Count - 1);
    }

    public bool IsFinished { get; private set; }

    /// <summary>Raised before the current step changes; set Cancel to veto, or to flag the step as invalid.</summary>
    public event EventHandler<StepChangingEventArgs>? Changing;
    public event EventHandler? Changed;

    public void Add(string title, string? description = null, string? glyph = null, bool optional = false)
    {
        Steps.Add(new StepEntry { Title = title, Description = description, Glyph = glyph, IsOptional = optional });
        if (Steps.Count == 1) Steps[0].Status = StepStatus.Active;
    }

    public bool CanNavigateTo(int index)
    {
        if (index < 0 || index >= Steps.Count) return false;
        return index <= Current || CanReachForward(index);
    }

    public bool TryNavigateTo(int index)
    {
        if (index == Current && !IsFinished) return true;
        if (index < 0 || index >= Steps.Count) return false;
        if (index > Current)
        {
            if (!CanReachForward(index)) return false;
            var args = new StepChangingEventArgs(Current, index);
            Changing?.Invoke(this, args);
            if (args.Cancel)
            {
                Steps[Current].Status = StepStatus.Error;
                Steps[Current].Message = args.Message;
                Changed?.Invoke(this, EventArgs.Empty);
                return false;
            }

            for (var i = Current; i < index; i++)
            {
                if (i == Current || Steps[i].Status == StepStatus.Pending) Steps[i].Status = i == Current ? StepStatus.Completed : StepStatus.Skipped;
                Steps[i].Message = null;
            }
        }
        else
        {
            var args = new StepChangingEventArgs(Current, index);
            Changing?.Invoke(this, args);
            if (args.Cancel) return false;
        }

        IsFinished = false;
        Current = index;
        for (var i = index; i < Steps.Count; i++) Steps[i].Status = i == index ? StepStatus.Active : Steps[i].Status == StepStatus.Completed ? StepStatus.Completed : StepStatus.Pending;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private bool CanReachForward(int index) => AllowJumpAhead || index == Current + 1 || Enumerable.Range(Current + 1, index - Current - 1).All(i => Steps[i].Status is StepStatus.Completed or StepStatus.Skipped || Steps[i].IsOptional);

    public bool Next()
    {
        if (Current < Steps.Count - 1) return TryNavigateTo(Current + 1);
        return Finish();
    }

    public bool Previous() => Current > 0 && TryNavigateTo(Current - 1);

    /// <summary>Validates the last step and marks every step completed.</summary>
    public bool Finish()
    {
        if (Steps.Count == 0) return false;
        var args = new StepChangingEventArgs(Current, Steps.Count);
        Changing?.Invoke(this, args);
        if (args.Cancel)
        {
            Steps[Current].Status = StepStatus.Error;
            Steps[Current].Message = args.Message;
            Changed?.Invoke(this, EventArgs.Empty);
            return false;
        }

        foreach (var s in Steps.Where(s => s.Status != StepStatus.Skipped)) s.Status = StepStatus.Completed;
        IsFinished = true;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void SetStatus(int index, StepStatus status, string? message = null)
    {
        if (index < 0 || index >= Steps.Count) return;
        Steps[index].Status = status;
        Steps[index].Message = message;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        IsFinished = false;
        Current = 0;
        for (var i = 0; i < Steps.Count; i++)
        {
            Steps[i].Status = i == 0 ? StepStatus.Active : StepStatus.Pending;
            Steps[i].Message = null;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Completed fraction 0..1 used to fill the connector line.</summary>
    public double Progress => Steps.Count <= 1 ? (IsFinished ? 1 : 0) : IsFinished ? 1 : (double)Current / (Steps.Count - 1);
}
