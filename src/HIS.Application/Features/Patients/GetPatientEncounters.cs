using HIS.Application.Abstractions;
using HIS.Application.Features.Opd;   // TemplateAnswerDto

namespace HIS.Application.Features.Patients;

/// <summary>One consultation in a patient's visit history, with its structured template answers.</summary>
public sealed record EncounterHistoryDto(
    long EncounterId, DateTime DateUtc, string? Doctor, string? Department,
    string? Diagnosis, string? Complaints, IReadOnlyList<TemplateAnswerDto> Answers);

public sealed record GetPatientEncountersQuery(string Uhid) : IQuery<IReadOnlyList<EncounterHistoryDto>>, IRequireAuthentication;

public sealed class GetPatientEncountersHandler : MediatR.IRequestHandler<GetPatientEncountersQuery, IReadOnlyList<EncounterHistoryDto>>
{
    private readonly IPatientRepository _patients;
    private readonly IEncounterRepository _enc;
    public GetPatientEncountersHandler(IPatientRepository patients, IEncounterRepository enc) { _patients = patients; _enc = enc; }

    public async Task<IReadOnlyList<EncounterHistoryDto>> Handle(GetPatientEncountersQuery q, CancellationToken ct)
    {
        var p = await _patients.GetByUhidAsync(q.Uhid, ct);
        if (p is null) return System.Array.Empty<EncounterHistoryDto>();

        var encs = await _enc.GetPatientEncountersAsync(p.PatientId, ct);
        var list = new List<EncounterHistoryDto>(encs.Count);
        foreach (var e in encs)
        {
            var answers = await _enc.GetTemplateAnswersAsync(e.EncounterId, ct);
            list.Add(new EncounterHistoryDto(e.EncounterId, e.StartedUtc, e.Doctor, e.Department, e.Diagnosis, e.Complaints,
                answers.Select(a => new TemplateAnswerDto(a.Label, a.FieldType, a.Value)).ToList()));
        }
        return list;
    }
}
