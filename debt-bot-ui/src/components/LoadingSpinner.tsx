import { Box, CircularProgress } from '@mui/material';

const LoadingSpinner = () => (
    <Box display="flex" justifyContent="center" alignItems="center" minHeight="200px">
        <CircularProgress />
    </Box>
);

export default LoadingSpinner;
