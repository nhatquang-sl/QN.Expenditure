namespace Lib.Application.Models;

public class PaginatedList<T>
{
    public List<T> Items { get; set; } = [];
    public int PageNumber { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
}
