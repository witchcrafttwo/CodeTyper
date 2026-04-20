using CodeTyper.Api.Models;

namespace CodeTyper.Api.Data;

public interface IModeCatalog
{
    IReadOnlyList<ModeDefinition> GetAll();
}
