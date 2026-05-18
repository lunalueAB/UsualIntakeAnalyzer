import React, { useState } from 'react'
import {Btn, Badge, Modal} from '../components/UI'

const DETAILS = {
  NCI: {
    title: 'NCI 방법 (National Cancer Institute Method)',
    sections: [
      { heading: '개요', content: 'NCI 방법은 미국 국립암연구소에서 개발한 일상섭취량 추정 방법으로, 24시간 회상 자료를 이용하여 개인 수준의 일상섭취량 분포를 추정합니다. 에피소드성 식품과 매일섭취 식품 모두에 적용 가능한 2-파트 혼합효과 모형입니다.' },
      { heading: '적용 대상', content: '• papa > 5% 또는 0섭취율 > 15%인 에피소드성 식품\n• 거의 매일 섭취하는 식품 (섭취자율 < 95%)\n• 2일 이상의 반복 측정 자료가 있는 경우 (반복조사 필수)' },
      { heading: '산출 원리', content: '로지스틱 회귀모형(섭취 여부)과 감마 GLM(섭취량)을 결합한 2-파트 모형. 몬테카를로 시뮬레이션으로 주 7일 섭취량을 생성하여 개인 수준 일상섭취량 분포를 추정합니다.' },
      { heading: '난수 시드', content: '시드값 20180412로 고정되어 동일한 입력 데이터와 조건에서 항상 동일한 결과를 산출합니다.' },
      { heading: '관련 근거 및 참고문헌', content: '① Tooze JA, et al. (2006). A new statistical method for estimating the usual intake of episodically consumed foods. J Am Diet Assoc, 106(10), 1575–1587.\n\n② Kipnis V, et al. (2009). Modeling data with excess zeros and measurement error: application to evaluating relationships between episodically consumed foods and health outcomes. Biometrics, 65(4), 1003–1010.\n\n③ National Cancer Institute. (2012). Usual Dietary Intakes: The NCI Method. https://epi.grants.cancer.gov/diet/usualintakes/method.html\n\n④ Dodd KW, et al. (2006). Statistical methods for estimating usual intake of nutrients and foods: a review of the theory. J Am Diet Assoc, 106(10), 1640–1650.' },
    ],
  },
  ISU: {
    title: 'ISU 방법 (Iowa State University Method)',
    sections: [
      { heading: '개요', content: 'ISU 방법은 아이오와주립대학교에서 개발한 방법으로, BLUP(Best Linear Unbiased Prediction) 기반으로 개인 간 분산과 개인 내 분산을 분리하여 일상섭취량을 추정합니다. 거의 매일 섭취하는 식품에 최적화되어 있습니다.' },
      { heading: '적용 대상', content: '• papa ≤ 5% AND 0섭취율 ≤ 15%인 매일섭취 식품\n• 거의 매일 섭취하는 식품 (섭취자율 ≥ 95%)\n• 정규 분포 또는 로그 변환 후 정규 분포에 가까운 자료' },
      { heading: '산출 원리', content: '신뢰도(reliability) = σ_b² / (σ_b² + σ_w²/n). 개인 내 변이를 제거하여 장기평균 섭취량을 추정합니다. 모든 값이 양수인 경우 로그 변환 후 적용합니다.' },
      { heading: '관련 근거 및 참고문헌', content: '① Nusser SM, et al. (1996). A semiparametric transformation approach to estimating usual daily intake distributions. J Am Stat Assoc, 91(436), 1440–1449.\n\n② Carriquiry AL. (1999). Assessing the prevalence of nutrient inadequacy. Public Health Nutr, 2(1), 23–33.\n\n③ Hoffmann K, et al. (2002). Estimating the distribution of usual dietary intake by short-term measurements. Eur J Clin Nutr, 56(Suppl 2), S53–62.\n\n④ Beaton GH, et al. (1979). Sources of variance in 24-hour dietary recall data: implications for nutrition study design and interpretation. Am J Clin Nutr, 32(12), 2546–2559.' },
    ],
  },
  MSM: {
    title: 'MSM 방법 (Multiple Source Method)',
    sections: [
      { heading: '개요', content: 'MSM 방법은 독일 EPIC(유럽암역학연구) 그룹에서 개발된 방법으로, 에피소드성 식품에 적합한 2-파트 BLUP 모형입니다. 섭취 여부와 섭취량을 분리하여 모델링합니다.' },
      { heading: '적용 대상', content: '• papa > 5% 또는 0섭취율 > 15%인 에피소드성 식품\n• 채소류, 과일류, 어패류 등 간헐적으로 섭취하는 식품군\n• 개인별 섭취 확률과 섭취량을 분리 추정해야 하는 경우' },
      { heading: '산출 원리', content: '섭취확률(p̂) × 섭취량 BLUP으로 일상섭취량을 추정합니다. 섭취 여부는 로지스틱 회귀, 섭취량은 선형 혼합 모형으로 각각 모형화하여 두 결과를 결합합니다.' },
      { heading: '관련 근거 및 참고문헌', content: '① Haubrock J, et al. (2011). Estimating usual food intake distributions by using the Multiple Source Method in the EPIC-Potsdam Calibration Study. J Nutr, 141(5), 914–920.\n\n② Harttig U, et al. (2011). The MSM program: web-based statistics package for estimating usual dietary intake using the Multiple Source Method. Eur J Clin Nutr, 65(S1), S87–S91.\n\n③ Souverein OW, et al. (2011). Comparing four methods to estimate usual intake distributions. Eur J Clin Nutr, 65(S1), S92–S101.\n\n④ Dodd KW, et al. (2006). Statistical methods for estimating usual intake of nutrients and foods: a review of the theory. J Am Diet Assoc, 106(10), 1640–1650.' },
    ],
  },
}

function MethodDetailDialog({ methodKey, onClose }) {
  const detail = DETAILS[methodKey]
  if (!detail) return null
  return (
    <Modal title={detail.title} onClose={onClose} width={640}>
      <div style={{ paddingTop: 16, display: 'flex', flexDirection: 'column', gap: 18 }}>
        {detail.sections.map((sec) => (
          <div key={sec.heading}>
            <div style={{ fontWeight: 700, fontSize: 13, marginBottom: 6, color: '#1E293B' }}>{sec.heading}</div>
            <div style={{
              fontSize: 12, color: '#475569', lineHeight: 1.9, whiteSpace: 'pre-line',
              background: sec.heading === '관련 근거 및 참고문헌' ? '#F0F9FF' : '#F8FAFC',
              borderRadius: 6, padding: '10px 14px',
              borderLeft: sec.heading === '관련 근거 및 참고문헌' ? '3px solid #2563EB' : 'none',
            }}>
              {sec.content}
            </div>
          </div>
        ))}
      </div>
    </Modal>
  )
}

export default function MethodologyTab({ onGoToAnalysis }) {
  const [openDetail, setOpenDetail] = useState(null)

  const methods = [
    {
      badge: '기본 방법', icon: '🎯', title: 'NCI', sub: 'National Cancer Institute Method',
      desc: '랜덤효과 2부 모형(2-part mixed model)으로 섭취자 비율과 섭취량 분포를 분리하여 추정합니다. 개인 내·개인 간 분산 분리를 통해 장기 섭취 분포를 Monte Carlo 시뮬레이션합니다.',
      cond: ['비연속 섭취 식품 (섭취자율 < 95%)', '2일 조사 자료 보유 (반복조사 필수)', '충분한 표본 크기 (N ≥ 300)'],
      color: '#2563EB', badge_color: 'blue',
    },
    {
      badge: '보완 방법 ①', icon: '📊', title: 'ISU', sub: 'Iowa State University Method',
      desc: 'Box-Cox 변환을 적용한 ANOVA 기반 방법입니다. 식품 섭취가 비교적 연속적이고 정규 분포에 가까울 때 적용합니다. BLUP(σ_b²/(σ_b²+σ_w²))를 이용해 개인별 일상섭취량을 추정합니다.',
      cond: ['준연속 섭취 (섭취자율 ≥ 95%)', 'papa ≤ 5%', '0섭취율 ≤ 15%'],
      color: '#7C3AED', badge_color: 'purple',
    },
    {
      badge: '보완 방법 ②', icon: '🧮', title: 'MSM', sub: 'Multiple Source Method',
      desc: '섭취 확률과 섭취량을 분리하여 two-part BLUP를 적용합니다. p̂(섭취 확률) × E(섭취량|섭취일) 형태로 추정하며 로그정규 분포 보정을 포함합니다.',
      cond: ['간헐적 섭취 (papa > 5% 또는 0섭취율 > 15%)', '어패류, 견과류, 특정 채소류', '개인별 BLUP 산출이 필요한 경우'],
      color: '#0891B2', badge_color: 'gray',
    },
  ]

  return (
    <div style={{ flex: 1, overflow: 'auto', paddingBottom: 40, minHeight: 0 }}>
      {openDetail && (
        <MethodDetailDialog methodKey={openDetail} onClose={() => setOpenDetail(null)} />
      )}
      <div style={{ maxWidth: 1000, margin: '0 auto', padding: '32px 40px' }}>
        <div style={{ marginBottom: 20 }}>
          <div style={{ fontSize: 26, fontWeight: 700, marginBottom: 6 }}>산출 방법론</div>
          <div style={{ fontSize: 13, color: '#64748B' }}>일상섭취량(usual intake) 추정에 사용되는 통계 방법 소개</div>
        </div>
        <div style={{
          borderLeft: '3px solid #2563EB', padding: '14px 18px', background: '#fff',
          borderRadius: '0 6px 6px 0', marginBottom: 28, lineHeight: 1.8, fontSize: 12,
        }}>
          <strong>일상섭취량(Usual Intake)</strong>은 1~2일치 식이조사 자료에서 개인의 평소(장기 평균) 섭취 분포를 추정하는 개념입니다.
          본 프로그램은 미국 NCI 방법을 기본으로 하며, 자료의 특성에 따라 ISU 또는 MSM 방법을 자동 선택하여 보완 적용합니다.
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3,1fr)', gap: 18, marginBottom: 28 }}>
          {methods.map((m) => (
            <div key={m.title} style={{
              border: '1px solid #E2E8F0', borderRadius: 8,
              background: '#fff', padding: '20px', display: 'flex', flexDirection: 'column', gap: 10,
            }}>
              <Badge color={m.badge_color}>{m.badge}</Badge>
              <div style={{ fontSize: 32 }}>{m.icon}</div>
              <div>
                <div style={{ fontSize: 20, fontWeight: 700, color: m.color }}>{m.title}</div>
                <div style={{ fontSize: 11, color: '#94A3B8', marginTop: 2 }}>{m.sub}</div>
              </div>
              <div style={{ fontSize: 12, color: '#475569', lineHeight: 1.7, flex: 1 }}>{m.desc}</div>
              <div style={{ borderTop: '1px solid #E2E8F0', paddingTop: 10 }}>
                <div style={{ fontSize: 11, fontWeight: 600, color: '#64748B', marginBottom: 6 }}>적용 조건</div>
                {m.cond.map((c) => (
                  <div key={c} style={{ fontSize: 11, color: '#475569', display: 'flex', gap: 6, marginBottom: 3 }}>
                    <span style={{ color: m.color, flexShrink: 0 }}>✓</span>{c}
                  </div>
                ))}
              </div>
              <button
                onClick={() => setOpenDetail(m.title)}
                style={{
                  marginTop: 4, background: 'none', border: '1px solid #CBD5E1',
                  borderRadius: 4, padding: '6px 12px', fontSize: 12, color: '#475569',
                  cursor: 'pointer', textAlign: 'center', fontFamily: 'inherit',
                }}
              >
                📖 자세히 보기
              </button>
            </div>
          ))}
        </div>
        <div style={{
          marginBottom: 24, padding: '12px 14px', background: '#DBEAFE',
          borderRadius: 6, fontSize: 12, color: '#1D4ED8',
        }}>
          <strong>난수 시드 고정 (seed=20180412)</strong> — 동일한 데이터·식품군·반복 횟수 조건에서 항상 동일한 결과가 출력됩니다.
        </div>
        <div style={{
          background: 'linear-gradient(135deg, #1a2a4a 0%, #2d4a7a 100%)',
          borderRadius: 8, padding: '28px 32px',
          display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 24,
        }}>
          <div>
            <div style={{ fontSize: 18, fontWeight: 700, color: '#fff', marginBottom: 8 }}>이제 분석을 시작하세요</div>
            <div style={{ fontSize: 13, color: '#a0b8d8', lineHeight: 1.6 }}>준비된 데이터와 식품군을 선택하여 일상섭취량 분석을 실행하세요.</div>
          </div>
          <button
            onClick={() => onGoToAnalysis && onGoToAnalysis()}
            style={{
              background: '#fff', color: '#2d4a7a', border: 'none',
              borderRadius: 6, padding: '12px 24px', fontSize: 14, fontWeight: 700,
              cursor: 'pointer', whiteSpace: 'nowrap', fontFamily: 'inherit', flexShrink: 0,
            }}
          >
            일상섭취량 분석으로 이동 →
          </button>
        </div>
      </div>
    </div>
  )
}
