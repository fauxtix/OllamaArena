using Dapper;
using System.Data;
using System.Globalization;

namespace OllamaArena.Services.Helpers;

/// <summary>
/// Registra handlers para o Dapper converter valores numéricos do SQLite.
/// O SQLite guarda números inteiros (ex.: 4) como INTEGER mesmo em colunas
/// REAL/NUMERIC, e o mapper predefinido do Dapper falha ao fazer cast direto
/// de Int64 para double/float (InvalidCastException).
/// </summary>
public static class SqliteTypeHandlers
{
    private static bool _registered;

    /// <summary>
    /// Regista os handlers uma única vez (idempotente, seguro para testes).
    /// Deve ser chamado antes da primeira query Dapper.
    /// </summary>
    public static void Register()
    {
        if (_registered) return;
        _registered = true;

        SqlMapper.AddTypeHandler(new DoubleHandler());
        SqlMapper.AddTypeHandler(new NullableDoubleHandler());
        SqlMapper.AddTypeHandler(new SingleHandler());
        SqlMapper.AddTypeHandler(new NullableSingleHandler());
    }

    private sealed class DoubleHandler : SqlMapper.TypeHandler<double>
    {
        public override void SetValue(IDbDataParameter parameter, double value)
            => parameter.Value = value;

        public override double Parse(object value)
            => Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }

    private sealed class NullableDoubleHandler : SqlMapper.TypeHandler<double?>
    {
        public override void SetValue(IDbDataParameter parameter, double? value)
            => parameter.Value = value ?? (object)DBNull.Value;

        public override double? Parse(object value)
            => value is null or DBNull ? null : Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }

    private sealed class SingleHandler : SqlMapper.TypeHandler<float>
    {
        public override void SetValue(IDbDataParameter parameter, float value)
            => parameter.Value = value;

        public override float Parse(object value)
            => Convert.ToSingle(value, CultureInfo.InvariantCulture);
    }

    private sealed class NullableSingleHandler : SqlMapper.TypeHandler<float?>
    {
        public override void SetValue(IDbDataParameter parameter, float? value)
            => parameter.Value = value ?? (object)DBNull.Value;

        public override float? Parse(object value)
            => value is null or DBNull ? null : Convert.ToSingle(value, CultureInfo.InvariantCulture);
    }
}
