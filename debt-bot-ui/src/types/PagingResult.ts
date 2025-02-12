export interface PagingResult<T> {
    countPerPage?: number;
    pageNumber: number;
    totalCount: number;
    lastPageNumber: number;
    isFirstPage: boolean;
    isLastPage: boolean;
    moreThenFivePassed: boolean;
    moreThenFiveLeft: boolean;
    items?: T[];
}
