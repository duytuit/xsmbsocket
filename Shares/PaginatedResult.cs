using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace xsmbsocket.Shares
{
    public class PaginatedResult<T>
    {
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        public List<T> Items { get; set; }
        public Dictionary<string, object> Extra { get; set; } = new Dictionary<string, object>();
    }
    public class PaginatedResultVue<T>
    {
        public int Current_page { get; set; }
        public int Last_page { get; set; }
        public int Total { get; set; }
        public int Per_page { get; set; }
        public List<T> Data { get; set; }
        public Dictionary<string, object> Extra { get; set; } = new Dictionary<string, object>();
    }
    public class PaginatedResultReact<T>
    {
        public int PageNum { get; set; }
        public int PageSize { get; set; }
        public int First { get; set; }
        public int Total { get; set; }
        public List<T> Data { get; set; }
        public Dictionary<string, object> Extra { get; set; } = new Dictionary<string, object>();
    }
}
