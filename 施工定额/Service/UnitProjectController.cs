using 施工定额.Entity;
using 施工定额.Helper;

namespace 施工定额.Service
{
    /// <summary>
    /// 单位工程业务协调器。UI 只负责交互和刷新，不直接编排多个仓储。
    /// </summary>
    public sealed class UnitProjectController
    {
        private readonly UnitProjectRepository _unitRepository;
        private readonly OtherProjectRepository _otherRepository;

        public UnitProjectController(UnitProjectRepository unitRepository, OtherProjectRepository otherRepository)
        {
            _unitRepository = unitRepository ?? throw new ArgumentNullException(nameof(unitRepository));
            _otherRepository = otherRepository ?? throw new ArgumentNullException(nameof(otherRepository));
        }

        public void EnsureDefault() => _unitRepository.EnsureDefault();

        public IReadOnlyList<UnitProject> ListAll() => _unitRepository.ListAll();

        public UnitProject Add(string name)
        {
            var project = _unitRepository.Add(name);
            _otherRepository.EnsureDefaultsForUnit(project.编码);
            return project;
        }

        public void Rename(string code, string name) => _unitRepository.Rename(code, name);

        public void Delete(string code) => _unitRepository.Delete(code);
    }
}
