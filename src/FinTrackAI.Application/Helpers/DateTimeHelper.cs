using System.Globalization;

namespace FinTrackAI.Application.Helpers;

public static class DateTimeHelper
{
    public static DateTime FromTimestampMs(long timestampMs)
        => DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).LocalDateTime;

    public static long ToTimestampMs(DateTime dateTime)
        => new DateTimeOffset(dateTime).ToUnixTimeMilliseconds();

    public static string FormatarMesAno(int mes, int ano)
        => $"{mes:D2}/{ano}";

    public static (long InicioMs, long FimExclusivoMs) IntervaloMesLocal(int mes, int ano)
    {
        DateTime inicio = new DateTime(ano, mes, 1, 0, 0, 0, DateTimeKind.Local);
        DateTime fimExclusivo = inicio.AddMonths(1);
        return (ToTimestampMs(inicio), ToTimestampMs(fimExclusivo));
    }

    public static string FormatarMoedaBr(double valor)
        => valor.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
}
