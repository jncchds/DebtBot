using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DebtBot.Models;
public class PagingResult<T>
{
    public int? CountPerPage { get; }
    public int PageNumber { get; }
    public int TotalCount { get; }
    public int LastPageNumber => (TotalCount - 1) / (CountPerPage ?? 1);

    public bool IsFirstPage => PageNumber == 0;
    public bool IsLastPage => PageNumber == LastPageNumber;

    public bool MoreThenFivePassed => PageNumber >= 5;
    public bool MoreThenFiveLeft => LastPageNumber - PageNumber >= 5;

    public List<T> Items { get; }

    public PagingResult(int? countPerPage, int pageNumber, int totalCount, List<T> items)
    {
        // To be sure we're not dividing by zero for TotalPages
        if (countPerPage == 0)
            countPerPage = 1;
        
        CountPerPage = countPerPage;


        PageNumber = pageNumber;
        TotalCount = totalCount;
        Items = items;
    }
}
