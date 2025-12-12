import { TabContext, TabList, TabPanel } from '@mui/lab';
import { Grid, Tab } from '@mui/material';
import { useState } from 'react';
import { SpotGridDto } from 'store/api-client';
import TabStatus from './status';
import TabSummary from './summary';

export default function Tabs(props: {
  lowerPrice: number;
  upperPrice: number;
  numberOfGrids: number;
  investment: number;
  spotGrid?: SpotGridDto;
}) {
  const [tabIndex, setTabIndex] = useState('summary');
  // const { lowerPrice, upperPrice, numberOfGrids, investment } = props;

  const handleChange = (_event: React.SyntheticEvent, newValue: string) => {
    setTabIndex(newValue);
  };

  return (
    <Grid container spacing={0}>
      <Grid item xs={12}>
        <TabContext value={tabIndex}>
          <TabList onChange={handleChange} aria-label="lab API tabs example">
            <Tab label="Summary" value="summary" />
            {props.spotGrid && <Tab label="Status" value="status" />}
            {props.spotGrid && <Tab label="History" value="history" />}
          </TabList>
          <TabPanel value="summary">
            <TabSummary {...props} />
          </TabPanel>
          <TabPanel value="status">
            {props.spotGrid && <TabStatus spotGrid={props.spotGrid} />}
          </TabPanel>
          <TabPanel value="history">Item Three</TabPanel>
        </TabContext>
      </Grid>
    </Grid>
  );
}
