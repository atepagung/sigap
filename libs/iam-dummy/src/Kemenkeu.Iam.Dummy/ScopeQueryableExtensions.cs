using System.Linq.Expressions;
using System.Reflection;

namespace Kemenkeu.Iam;

/// <summary>
/// Lapis 2 (Scope). [ASUMSI] Nama dan signature helper ini tebakan kita — lihat DUMMY_REGISTRY.md.
/// </summary>
public static class ScopeQueryableExtensions
{
    private static readonly MethodInfo ContainsString = typeof(Enumerable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
        .MakeGenericMethod(typeof(string));

    /// <summary>
    /// Menyisipkan filter lingkup ke query sebagai klausa WHERE. Tidak pernah memuat baris ke
    /// memori: yang dihasilkan hanya ekspresi, diterjemahkan provider EF Core menjadi SQL.
    /// </summary>
    /// <param name="unit">Kolom unit baris (<c>{unit}</c> di PERMISSION_MAP), untuk UNIT/WILAYAH/ESELON_I.</param>
    /// <param name="owner">Kolom pemilik baris (<c>{pemilik}</c>), untuk SELF.</param>
    /// <remarks>
    /// Gabungan antarperan adalah OR. Hanya profil generik yang diterapkan; profil domain dan
    /// profil yang kolomnya tidak disebut dilewati. Karena gabungannya OR, melewati sebuah profil
    /// hanya bisa MENYEMPITKAN hasil, tidak pernah melebarkannya. Tanpa satu pun profil yang
    /// berlaku, hasilnya kosong (fail-closed).
    /// </remarks>
    public static IQueryable<T> ApplyScope<T>(
        this IQueryable<T> source,
        DataScope scope,
        Expression<Func<T, string?>>? unit = null,
        Expression<Func<T, string?>>? owner = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(scope);
        if (unit is null && owner is null)
        {
            throw new ArgumentException("Sebutkan minimal satu kolom lingkup: unit atau owner.");
        }

        var generic = scope.Grants.Where(g => g.IsGeneric).ToList();
        if (generic.Any(g => g.Area.IsNational))
        {
            return source;
        }

        var values = new ScopeValues
        {
            UnitIds = unit is null ? [] : generic.SelectMany(g => g.Area.UnitIds).Distinct().ToArray(),
            OwnerIds = owner is null ? [] : generic.Select(g => g.Area.OwnerUserId).OfType<string>().Distinct().ToArray(),
        };

        var row = Expression.Parameter(typeof(T), "baris");
        Expression? filter = null;
        if (values.UnitIds.Length > 0)
        {
            filter = Or(filter, IsIn(values, nameof(ScopeValues.UnitIds), Rebind(unit!, row)));
        }
        if (values.OwnerIds.Length > 0)
        {
            filter = Or(filter, IsIn(values, nameof(ScopeValues.OwnerIds), Rebind(owner!, row)));
        }

        return source.Where(Expression.Lambda<Func<T, bool>>(filter ?? Expression.Constant(false), row));
    }

    private static Expression Or(Expression? left, Expression right) =>
        left is null ? right : Expression.OrElse(left, right);

    // Akses field pada objek penampung membuat EF Core memperlakukan daftar nilai sebagai
    // parameter SQL (mis. "= ANY (@unitIds)" di PostgreSQL), bukan literal yang ditempel ke teks query.
    private static Expression IsIn(ScopeValues values, string field, Expression column) =>
        Expression.Call(ContainsString, Expression.Field(Expression.Constant(values), field), column);

    private static Expression Rebind<T>(Expression<Func<T, string?>> selector, ParameterExpression row) =>
        new ParameterSwap(selector.Parameters[0], row).Visit(selector.Body);

    private sealed class ScopeValues
    {
        public string[] UnitIds = [];
        public string[] OwnerIds = [];
    }

    private sealed class ParameterSwap(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
