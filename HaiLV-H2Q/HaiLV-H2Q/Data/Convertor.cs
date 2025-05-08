using System.Data;

namespace HaiLV_H2Q.Data
{
    public static class Convertor
    {
        public static List<T> ToList<T>(DataTable dt, Func<DataRow, T> mapper)
        {
            if (dt == null) return new List<T>();
            return dt.AsEnumerable().Select(mapper).ToList();
        }
    }
}