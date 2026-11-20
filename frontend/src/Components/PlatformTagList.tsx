import React from 'react';
import { useTagList } from 'Tags/useTags';
import TagList from './TagList';

interface PlatformTagListProps {
  tags: number[];
}

function PlatformTagList({ tags }: PlatformTagListProps) {
  const tagList = useTagList();

  return <TagList tags={tags} tagList={tagList} />;
}

export default PlatformTagList;
