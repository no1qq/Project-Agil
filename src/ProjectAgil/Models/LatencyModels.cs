namespace ProjectAgil.Models;

public sealed class LatencyStats
{
    public string Host { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public double Current { get; init; }

    public double Average { get; init; }

    public double Minimum { get; init; }

    public double Maximum { get; init; }

    public double Jitter { get; init; }

    public double Loss { get; init; }

    public int Sent { get; init; }

    public int Answered { get; init; }

    public int Refused { get; init; }

    public bool Online { get; init; }

    public bool Refusing { get; init; }

    public string CurrentDisplay =>
        Online ? $"{Current:0} ms"
        : Refusing ? "refused"
        : "timeout";

    public string AverageDisplay => Sent == 0 ? "-" : $"{Average:0.0} ms";

    public string JitterDisplay => Sent == 0 ? "-" : $"{Jitter:0.0} ms";

    public string LossDisplay => Sent == 0 ? "-" : $"{Loss:0.#} %";

    public string RangeDisplay => Sent == 0 ? "-" : $"{Minimum:0} / {Maximum:0} ms";

    public int Grade
    {
        get
        {
            if (Sent == 0 || Answered == 0)
            {
                return 0;
            }

            var distance = Average switch
            {
                <= 30 => 0,
                <= 60 => (Average - 30) * 0.30,
                <= 100 => 9 + ((Average - 60) * 0.30),
                <= 160 => 21 + ((Average - 100) * 0.20),
                _ => 33 + Math.Min(22, (Average - 160) * 0.15),
            };

            var steadiness = Math.Min(30, Jitter * 3.0);
            var reliability = Math.Min(45, Loss * 5.0);

            return (int)Math.Clamp(100 - distance - steadiness - reliability, 0, 100);
        }
    }

    public string GradeLabel =>
        Answered == 0 && Refused > 0
            ? "not answering"
            : Grade switch
            {
                >= 85 => "Excellent",
                >= 70 => "Good",
                >= 50 => "Fair",
                >= 25 => "Poor",
                _ => "Bad",
            };

    public double RefusedShare => Sent == 0 ? 0 : Refused * 100d / Sent;

    public bool RefusalIsWorthMentioning => Refused > 0 && (Refusing || RefusedShare >= NoteworthyRefusedPercent);

    public string RefusedNote =>
        !RefusalIsWorthMentioning
            ? string.Empty
            : $"{Refused} of {Sent} checks were turned away by the server, so they are not counted as lost packets.";

    private const double NoteworthyRefusedPercent = 5d;
}
