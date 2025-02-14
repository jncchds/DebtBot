export enum ProcessingState {
    Draft = 0,
    Ready = 1,
    Processed = 3,
    Cancelled = 4,
}

export const getStatusLabel = (status: ProcessingState): string => {
    switch (status) {
        case ProcessingState.Draft:
            return 'Draft';
        case ProcessingState.Ready:
            return 'Ready';
        case ProcessingState.Processed:
            return 'Processed';
        case ProcessingState.Cancelled:
            return 'Cancelled';
        default:
            return 'Unknown';
    }
};

export const getStatusColor = (status: ProcessingState): string => {
    switch (status) {
        case ProcessingState.Draft:
            return '#FFA726'; // Orange
        case ProcessingState.Ready:
            return '#66BB6A'; // Green
        case ProcessingState.Processed:
            return '#2196F3'; // Blue
        case ProcessingState.Cancelled:
            return '#EF5350'; // Red
        default:
            return '#757575'; // Grey
    }
};
