using static DriveCrawl_Win32_App.MainWindow;

namespace DriveCrawl_Win32_App
{
    /// <summary>
    /// Post-search criteria (name, bitrate, video height) parsed from raw UI text.
    /// Kept free of WPF controls so the matching rules can be unit tested.
    /// </summary>
    public class SearchResultFilter
    {
        private readonly string _normalizedName;
        private readonly bool _isBitrateFilterActive;
        private readonly int _targetBitrate;
        private readonly Operator _bitrateOperator;
        private readonly bool _isVideoHeightFilterActive;
        private readonly int _targetVideoHeight;
        private readonly Operator _videoHeightOperator;

        public bool IsActive => !string.IsNullOrEmpty(_normalizedName) || _isBitrateFilterActive || _isVideoHeightFilterActive;

        public SearchResultFilter(string? nameText, string? bitrateText, Operator bitrateOperator, string? videoHeightText, Operator videoHeightOperator)
        {
            _normalizedName = nameText?.Trim().ToLowerInvariant() ?? string.Empty;
            _isBitrateFilterActive = int.TryParse(bitrateText?.Trim() ?? string.Empty, out _targetBitrate);
            _bitrateOperator = bitrateOperator;
            _isVideoHeightFilterActive = int.TryParse(videoHeightText, out _targetVideoHeight);
            _videoHeightOperator = videoHeightOperator;
        }

        public bool Matches(FileViewModel file)
        {
            return MatchesName(file) && MatchesBitrate(file) && MatchesVideoHeight(file);
        }

        private bool MatchesName(FileViewModel file)
        {
            return string.IsNullOrEmpty(_normalizedName) ||
                   (file.FileName != null && file.FileName.ToLowerInvariant().Contains(_normalizedName));
        }

        private bool MatchesBitrate(FileViewModel file)
        {
            if (!_isBitrateFilterActive)
                return true;

            if (!file.BitrateKbps.HasValue)
                return false;

            int fileBitrate = file.BitrateKbps.Value;
            return _bitrateOperator switch
            {
                Operator.GreaterThan => fileBitrate > _targetBitrate,
                Operator.LessThan => fileBitrate < _targetBitrate,
                Operator.Equals => fileBitrate == _targetBitrate,
                _ => true
            };
        }

        private bool MatchesVideoHeight(FileViewModel file)
        {
            // A non-positive height means "no constraint" even though it parsed, matching the original UI behaviour.
            if (!_isVideoHeightFilterActive || _targetVideoHeight <= 0)
                return true;

            if (file.VideoHeight == 0)
                return false;

            return _videoHeightOperator switch
            {
                Operator.GreaterThan => file.VideoHeight >= _targetVideoHeight,
                Operator.LessThan => file.VideoHeight <= _targetVideoHeight,
                Operator.Equals => file.VideoHeight == _targetVideoHeight,
                _ => true
            };
        }
    }
}
