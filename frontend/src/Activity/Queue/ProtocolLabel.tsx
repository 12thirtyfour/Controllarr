import React from 'react';
import Label, { LabelProps } from 'Components/Label';
import DownloadProtocol from 'DownloadClient/DownloadProtocol';
import styles from './ProtocolLabel.module.css';

interface ProtocolLabelProps
  extends Omit<LabelProps, 'children' | 'className'> {
  protocol: DownloadProtocol;
  showSourceName?: boolean;
}

function ProtocolLabel({
  protocol,
  showSourceName = false,
  ...otherProps
}: ProtocolLabelProps) {
  let protocolName: string = protocol;

  if (showSourceName) {
    if (protocol === 'http') {
      protocolName = 'Internet Archive';
    } else if (protocol === 'usenet') {
      protocolName = 'Usenet';
    } else if (protocol === 'torrent') {
      protocolName = 'Torrent';
    } else {
      protocolName = 'Unknown';
    }
  } else if (protocol === 'usenet') {
    protocolName = 'nzb';
  } else if (protocol === 'http') {
    protocolName = 'https';
  }

  const styleKey = protocol === 'http' ? 'torrent' : protocol;

  return (
    <Label className={styles[styleKey]} {...otherProps}>
      {protocolName}
    </Label>
  );
}

export default ProtocolLabel;
