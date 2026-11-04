using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.Tv
{
    public interface IPlatformTranslationService
    {
        List<PlatformTranslation> GetTranslations();
        List<PlatformTranslation> GetTranslations(int seriesId);
        List<PlatformTranslation> UpdateTranslations(int seriesId, List<PlatformTranslation> translations);
    }

    public class PlatformTranslationService : IPlatformTranslationService, IHandleAsync<SeriesDeletedEvent>
    {
        private readonly IPlatformTranslationRepository _seriesTranslationRepository;
        private readonly Logger _logger;

        public PlatformTranslationService(IPlatformTranslationRepository seriesTranslationRepository, Logger logger)
        {
            _seriesTranslationRepository = seriesTranslationRepository;
            _logger = logger;
        }

        public List<PlatformTranslation> GetTranslations()
        {
            return _seriesTranslationRepository.All().ToList();
        }

        public List<PlatformTranslation> GetTranslations(int seriesId)
        {
            return _seriesTranslationRepository.GetTranslations(seriesId);
        }

        public List<PlatformTranslation> UpdateTranslations(int seriesId, List<PlatformTranslation> translations)
        {
            var toAdd = new List<PlatformTranslation>();
            var toUpdate = new List<PlatformTranslation>();
            var existing = GetTranslations(seriesId);

            translations.ForEach(translation =>
            {
                var existingTranslation = existing.FirstOrDefault(e => e.Language == translation.Language);

                translation.SeriesId = seriesId;

                if (existingTranslation == null)
                {
                    toAdd.Add(translation);
                }
                else
                {
                    translation.Id = existingTranslation.Id;

                    toUpdate.Add(translation);
                    existing.Remove(existingTranslation);
                }
            });

            _seriesTranslationRepository.InsertMany(toAdd);
            _seriesTranslationRepository.UpdateMany(toUpdate);
            _seriesTranslationRepository.DeleteMany(existing);

            return translations;
        }

        public void HandleAsync(SeriesDeletedEvent message)
        {
            _seriesTranslationRepository.DeleteTranslations(message.Series.Select(s => s.Id).ToList());
        }
    }
}
