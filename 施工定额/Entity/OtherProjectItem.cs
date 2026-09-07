using System.ComponentModel;

namespace 施工定额.Entity
{
    /// <summary>其他项目（存于清单表，项目类别=其他项目）。</summary>
    public class OtherProjectItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private string _名称 = "";
        private decimal _金额;

        /// <summary>名称：暂列金额 / 暂估价 / 自定义…</summary>
        public string 名称
        {
            get => _名称;
            set { if (_名称 == value) return; _名称 = value; OnPropertyChanged(nameof(名称)); }
        }

        /// <summary>对应清单.综合合价。</summary>
        public decimal 金额
        {
            get => _金额;
            set { if (_金额 == value) return; _金额 = value; OnPropertyChanged(nameof(金额)); }
        }

        /// <summary>内部稳定编码，对应清单编码。</summary>
        public string 清单编码 { get; set; } = "";

        /// <summary>暂估价暂不可改金额（功能未做）。</summary>
        public bool IsAmountEditable =>
            名称 != "暂估价" && !OtherProjectRepository.IsZgjCode(清单编码);

        /// <summary>名称是否可改（暂估价固定）。</summary>
        public bool IsNameEditable =>
            !OtherProjectRepository.IsZgjCode(清单编码) && 名称 != "暂估价";
    }
}
