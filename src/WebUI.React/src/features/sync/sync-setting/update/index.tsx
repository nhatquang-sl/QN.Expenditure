import { BackdropLoading } from 'components/backdrop-loading';
import { useParams } from 'react-router-dom';
import SyncSettingForm from '../form';
import { useGetSyncSettings } from '../hooks/use-get-sync-settings';

export default function SyncSettingUpdate() {
  const { symbol } = useParams<{ symbol: string }>();
  const { data: syncSettings, isLoading } = useGetSyncSettings();

  if (isLoading) {
    return <BackdropLoading loading={true} />;
  }

  const syncSetting = syncSettings?.find((setting) => setting.symbol === symbol);

  return <SyncSettingForm syncSetting={syncSetting} />;
}
