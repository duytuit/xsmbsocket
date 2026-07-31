using System;
using System.Linq;
using System.Linq.Expressions;

public static class IQueryableExtensions
{
    public static IQueryable<T> WhereDateInRange<T>(
        this IQueryable<T> query,
        Expression<Func<T, DateTime>> dateSelector,
        DateTime? fromDate,
        DateTime? toDate)
    {
        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            query = query.Where(Expression.Lambda<Func<T, bool>>(
                Expression.GreaterThanOrEqual(
                    dateSelector.Body,
                    Expression.Constant(from)
                ),
                dateSelector.Parameters
            ));
        }

        if (toDate.HasValue)
        {
            var to = toDate.Value.Date.AddDays(1); // lấy hết ngày ToDate
            query = query.Where(Expression.Lambda<Func<T, bool>>(
                Expression.LessThan(
                    dateSelector.Body,
                    Expression.Constant(to)
                ),
                dateSelector.Parameters
            ));
        }

        return query;
    }
}
