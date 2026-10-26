import Language from 'Language/Language';
import Series from 'Platform/Platform';

interface Folder {
  language: Language;
  folder: string;
}

interface AddPlatform extends Series {
  folder: string;
  folders: Folder[];
  isExcluded: boolean;
}

export default AddPlatform;
