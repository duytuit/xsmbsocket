using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace xsmbsocket.Shares.MysqlHelper
{
    /// <summary>
    /// Đại diện cho một quan hệ giữa bảng cha và bảng con trong quá trình truy vấn dữ liệu theo cấu trúc động.
    /// </summary>
    public class AdoRelation
    {
        /// <summary>
        /// Tên property sẽ được dùng làm key trong kết quả trả về (ví dụ: "products").
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Tên bảng con cần truy vấn.
        /// </summary>
        public string Table { get; set; }

        /// <summary>
        /// Danh sách các cột cần lấy từ bảng con.
        /// </summary>
        public string[] Columns { get; set; }

        /// <summary>
        /// Tên cột trong bảng cha dùng để đối chiếu (ví dụ: "id").
        /// </summary>
        public string ParentKey { get; set; }

        /// <summary>
        /// Tên cột trong bảng con trỏ về bảng cha (khóa ngoại).
        /// </summary>
        public string ForeignKey { get; set; }

        /// <summary>
        /// Tên cột trong bảng con dùng để lọc dữ liệu bằng điều kiện WHERE IN.
        /// </summary>
        public string KeyName { get; set; }

        /// <summary>
        /// Xác định kết quả quan hệ là một danh sách (nhiều bản ghi) hay một đối tượng duy nhất.
        /// </summary>
        public bool IsCollection { get; set; } = true;

        /// <summary>
        /// Cho phép sử dụng Redis để cache kết quả của quan hệ.
        /// </summary>
        public bool UseRedisCache { get; set; } = false;

        /// <summary>
        /// Tiền tố để tạo key Redis cho việc lưu cache.
        /// </summary>
        public string RedisCacheKeyPrefix { get; set; }

        /// <summary>
        /// Thời gian tồn tại của cache Redis.
        /// </summary>
        public TimeSpan? RedisCacheDuration { get; set; }

        /// <summary>
        /// Danh sách các quan hệ con bên trong quan hệ hiện tại (hỗ trợ quan hệ lồng nhau).
        /// </summary>
        public List<AdoRelation> SubRelations { get; set; }
    }
}
