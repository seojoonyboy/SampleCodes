from django.http import JsonResponse, HttpResponse
from django.shortcuts import render, redirect
from django.urls import reverse_lazy

from site_members.models import Employee, Team, ORG_LEVEL
from site_works import models as works_models
from . import models
from site_monitor.forms import UserLoginForm
from django.contrib.auth import ( authenticate, login, logout )
from django.views.generic import ListView, FormView
from django.core.mail import send_mail
from django.template.loader import render_to_string
from django.contrib.auth.mixins import UserPassesTestMixin
from django.utils import timezone
from django import forms

import json
import csv
from urllib.parse import urlencode



class CACheckMixin(UserPassesTestMixin):
    login_url = reverse_lazy('monitor_login')
    def test_func(self):
        return self.is_ca(self.request.user)

    @staticmethod
    def is_ca(user):
        return hasattr(user, 'monitor_right')


# default : 1회차 미션
# 3회차 미션 클릭시 : 3회차 미션
class MainView(CACheckMixin, ListView):
    template_name = 'site_monitor/category_ratio.html'
    paginate_by = 10

    def get_queryset(self):
        user = self.request.user
        right = user.monitor_right
        orgs = right.authed_departments.all()
        lev = orgs[0].org_level
        if lev>=2:
            return Team.objects.none()
        lev_name = orgs[0].org_level_name
        in_query = dict()
        in_query[lev_name + '__in'] = [o.org_name for o in orgs]
        return Team.objects.annotate(rank_point=models.models.Sum('team_point_list__point'))\
            .filter(**in_query).order_by('-rank_point')

    def get_context_data(self, *, object_list=None, **kwargs):
        turn = self.kwargs.get('turn',1)
        context = super().get_context_data(object_list=object_list, **kwargs)
        user = self.request.user
        if not hasattr(user, 'monitor_right'):
            return redirect('monitor_login')
        right = user.monitor_right
        orgs = right.authed_departments.all()
        work = works_models.Works.objects.get(turn=turn)
        answers = _make_search_query(self.request, work.turn)
        ctg1, ctg2 = _get_ratio(answers, turn)
        context['turn'] = turn
        context['research'] = {1: json.dumps(list(ctg1)), 2: json.dumps(list(ctg2))}
        context['orgs'] = orgs
        context['lev'] = orgs[0].org_level
        context['org_levels'] = zip(ORG_LEVEL[:-1], ['본부', '공장', '실', '팀'])
        context['answer_count'] = answers.count()
        context['is_master'] = True if orgs.count() > 10 else False
        lev_name = orgs[0].org_level_name
        lev = orgs[0].org_level
        in_query = dict()
        in_query[lev_name + '__in'] = [o.org_name for o in orgs]
        team_queryset = Team.objects.filter(**in_query)
        if lev<2:
            context['attend_all'] = get_attend_rate(team_queryset)
            context['attend'] = dict()
            for hq in orgs:
                context['attend'][hq.org_name] = get_attend_rate(team_queryset.filter(hq=hq.org_name))

        return context

def get_attend_rate(team_queryset):
    member_queryset = Employee.objects.annotate(answer_count=models.models.Count('answers_done')).filter(team__in=team_queryset)
    # answer_count = works_models.LeaderAnswer.objects.filter(team__in=team_queryset).count()
    # TODO: LeaderAnswer 삭제됨
    answer_count = 0
    answer_count += member_queryset.aggregate(models.models.Sum('answer_count'))['answer_count__sum'] or 0
    people_total = member_queryset.count()
    people_attend_queryset = member_queryset.filter(answer_count__gt=0)
    people_attend = people_attend_queryset.count()
    team_total = team_queryset.count()
    team_attend = people_attend_queryset.values('team').distinct().count()
    return {'answer':answer_count,
            'people':{'total':people_total,'attend':people_attend},
            'team':{'total':team_total,'attend':team_attend}}


def attend_sub(request):
    hq = request.GET.get('hq',None)
    departments = Team.objects.filter(hq=hq).values('department').distinct()
    context = dict()
    for d in departments:
        dep = d['department']
        queryset = Team.objects.filter(hq=hq)
        if dep:
            queryset = queryset.filter(department=dep)
        else:
            queryset = queryset.filter(department__in=[None,''])
        context[dep if dep else '(실 없음)'] = get_attend_rate(queryset)

    return render(request, 'site_monitor/attend_sub.html', {'context':context})

def _get_ratio(answers, turn):
    class CatergoryData:
        def __init__(self, name, num):
            self.name = name
            self.num = num
    work = works_models.Works.objects.get(turn=turn)
    categories = work.answer_category.all()
    ctg_1st = dict()
    ctg_1st['etc'] = CatergoryData('없음', 0)
    ctg_2nd = dict()
    ctg_2nd['etc'] = CatergoryData('없음', 0)
    for ctg in categories:
        data = CatergoryData(ctg.name, 0)
        if ctg.category_type == 1:
            ctg_1st[ctg.id] = data
        else:
            ctg_2nd[ctg.id] = data

    for answer in answers:
        ctg1 = answer.category_1st
        ctg2 = answer.category_2nd
        if ctg1:
            ctg_1st[ctg1.id].num +=1
        else:
            ctg_1st['etc'].num +=1
        if ctg2:
            ctg_2nd[ctg2.id].num += 1
        else:
            ctg_2nd['etc'].num += 1
    ctg_1st = map(lambda d:[d.name,d.num], ctg_1st.values())
    ctg_2nd = map(lambda d:[d.name,d.num], ctg_2nd.values())
    return ctg_1st, ctg_2nd

def _make_search_query(request, work):
    user = request.user
    right = user.monitor_right
    orgs = right.authed_departments.all()
    lev = orgs[0].org_level
    lev_name = orgs[0].org_level_name
    work_turn = work
    work_queryset = works_models.Works.objects.all()
    try:
        work = work_queryset.get(turn=work_turn)
    except works_models.Works.DoesNotExist:
        work = work_queryset.order_by('turn').first()
    hq = request.GET.get('hq', None)
    team_queryset = Team.objects.all()
    in_query = dict()
    in_query[lev_name + '__in'] = [o.org_name for o in orgs]
    team_queryset = team_queryset.filter(**in_query)
    if lev <= 0 and hq:
        team_queryset = team_queryset.filter(hq=hq)
    factory = request.GET.get('factory', None)
    if lev <= 1 and factory and factory != '(없음)':
        team_queryset = team_queryset.filter(factory=factory)
    department = request.GET.get('department', None)
    if lev <= 2 and department and department != '(없음)':
        team_queryset = team_queryset.filter(department=department)
    if lev == 3:
        team_name = request.GET.get('team', None)
        if team_name:
            team_queryset = team_queryset.filter(team=team_name)
    else:
        team_id = request.GET.get('team', None)
        if team_id:
            team_queryset = team_queryset.filter(id=team_id)

    team_members = Employee.objects.filter(team__in=team_queryset)
    result_answers = work.answers.filter(employeeanswer__answer_by__in=team_members).order_by(
        'employeeanswer__answer_by__team')

    start = request.GET.get('start', None)
    if start:
        start = timezone.datetime.strptime(start,'%Y.%m.%d')
        result_answers = result_answers.filter(regdate__gte=start)
    end = request.GET.get('end', None)
    if end:
        end = timezone.datetime.strptime(end, '%Y.%m.%d')
        end += timezone.timedelta(days=1)
        result_answers = result_answers.filter(regdate__lt=end)

    return result_answers


class SearchWorks(CACheckMixin, ListView):
    template_name = 'site_monitor/search_work.html'
    paginate_by = 10

    def get_queryset(self):
        user = self.request.user
        right = user.monitor_right
        orgs = right.authed_departments.all()
        lev = orgs[0].org_level
        if lev == 3:
            work = self.request.GET.get('work',2)
        else:
            work = self.request.GET.get('work',1)
        return _make_search_query(self.request, work)

    def get_context_data(self, *, object_list=None, **kwargs):
        context = super().get_context_data(object_list=object_list, **kwargs)
        user = self.request.user
        right = user.monitor_right
        context['orgs'] = right.authed_departments.all()
        context['lev'] = context['orgs'][0].org_level
        context['org_levels'] = zip(ORG_LEVEL[:-1],['본부', '공장', '실', '팀'])
        # if context['lev'] == 3:
        #     context['mission'] = works_models.Works.objects.filter(for_leader=True)
        # else:
        #     context['mission'] = works_models.Works.objects.all()
        context['mission'] = works_models.Works.objects.all()
        context['get'] = self.request.GET.dict()
        context['get']['work'] = int(self.request.GET.get('work',1))
        if 'page' in context['get']:
            del context['get']['page']
        context['querystr'] = urlencode(context['get'])
        return context

    def render_to_response(self, context, **response_kwargs):
        return super().render_to_response(context, **response_kwargs)


def total_statistic(request):
    user = request.user
    if not hasattr(user, 'monitor_right'):
        return redirect('login')

    team_id = request.GET.get('team_id',None)
    result = get_statistic_result(team_id)
    user = request.user
    right = user.monitor_right
    orgs =  right.authed_departments.all()
    lev = orgs[0].org_level

    return render(request, 'site_monitor/total_statistic.html', {
        'result' : result,
        'orgs' : orgs,
        'lev': lev
    })

# select 태그에서 option 선택시 호출
# ajax 로 접근
def get_assigned_list(request):
    get = request.GET.dict()
    data = {
        'data':list(Team.objects.filter_teams(**get))
    }

    return JsonResponse(data)


def make_init_list():
    result = Team.objects.get_hq_list()
    return result


# 해당 과제를 Model 에서 검색
# def search_work(hq_name, dep_fac_type, dep_fac_name, team_name):
#     result = Team.objects.get_searched_works(hq_name, dep_fac_type, dep_fac_name, team_name)
#     return result


def login_view(request):
    form = UserLoginForm(request.POST or None)
    if request.method == 'POST':
        if form.is_valid():
            username = form.cleaned_data.get('username')
            password = form.cleaned_data.get('password')
            user = authenticate(username=username, password=password)
            if not hasattr(user,'monitor_right'):
                return redirect('main')
            login(request, user)
            right = user.monitor_right
            orgs = right.authed_departments.all()
            lev = orgs[0].org_level
            if lev == 3:
                return redirect('monitor_search_works')
            return redirect('monitor_main')
    return render(request, 'site_monitor/login.html', {
        'form' : form,
    })


def logout_view(request):
    logout(request)
    return redirect('monitor_login')


def get_statistic_result(team_id):
    # 임시 Team 하나
    # 팀원 수, 획득 포인트 평균
    # 전체 과제 수행(WA, 팀원)
    # 입력하는 과제답변 개수 평균
    # 획득 포인트 평균 (회차)
    # 전체 팀 획득 포인트
    if team_id:
        team = Team.objects.get(id=team_id)
    else:
        team = Team.objects.all().first()
    employees = team.team_member.all()
    member_num = employees.count()
    total_point_by_works = team.get_earned_point()
    work_count = works_models.Works.objects.all().count()

    if total_point_by_works is None:
        total_point_by_works = 0

    #TODO: 모니터링 페이지 workcount 수정
    works = works_models.Works.objects.filter(for_leader=False)
    total_work_count = 0
    works = works_models.Works.objects.filter(for_leader=True)
    wa_work_count = 0


    total_points = team.total_point['point__sum'] or 0
    total_points_per_turn = total_points / work_count
    answer_per_turn = total_work_count / work_count

    result = dict()
    result['team'] = team
    result['member_num'] = member_num
    result['wa_work_count'] = wa_work_count
    result['member_work_count'] = total_work_count
    result['total_points_per_turn'] = total_points_per_turn
    result['answer_per_turn'] = answer_per_turn
    result['total_points'] = total_points

    return result

def download_csv(request):
    user = request.user
    right = user.monitor_right
    orgs = right.authed_departments.all()
    lev = orgs[0].org_level
    response = HttpResponse(content_type='text/csv', charset='euc-kr')
    date = timezone.datetime.now().strftime('%Y%m%d%H%M')
    response['Content-Disposition'] = 'attachment; filename="answerlist_{}.csv"'.format(date)
    writer = csv.writer(response)
    writer.writerow(['회차','본부','공장','실','팀','그룹','작성날짜','카테고리1','카테고리2','내용'])
    works_queryset = works_models.Works.objects.all()
    work_turn = request.GET.get('work', None)
    if work_turn:
        works_queryset = works_queryset.filter(turn=work_turn)

    for work in works_queryset:
        if lev==3 and not work.for_leader:
            continue
        queryset = _make_search_query(request, work.turn)
        for row in queryset:
            writer.writerow([
                work.turn,
                row.get_team.hq,
                row.get_team.factory or '(없음)',
                row.get_team.department or '(없음)',
                row.get_team.team or '(없음)',
                row.get_team.group or '(없음)',
                row.regdate.strftime('%Y-%m-%d'),
                row.category_1st.name,
                row.category_2nd.name,
                row.content
            ])

    return response

def user_down(request):
    response = HttpResponse(content_type='text/csv', charset='euc-kr')
    date = timezone.datetime.now().strftime('%Y%m%d%H%M')
    response['Content-Disposition'] = 'attachment; filename="userlist_{}.csv"'.format(date)
    writer = csv.writer(response)
    user_queryset = Employee.objects.all().order_by('team')
    writer.writerow(['WA여부','이메일','닉네임','본부','공장','실','팀','그룹'])
    for row in user_queryset:
        writer.writerow([
            'WA' if row.is_team_leader else '',
            row.username,
            row.last_name,
            row.team.hq,
            row.team.factory if row.team.factory else '',
            row.team.department if row.team.department else '',
            row.team.team if row.team.team else '',
            row.team.group if row.team.group else ''
        ])
    return response

class SendMailForm(forms.Form):
    target_hq = forms.CharField()
    title = forms.CharField()
    content = forms.CharField()


class SendMail(FormView):
    form_class = SendMailForm
    template_name = 'site_monitor/send_mail.html'
    success_url = reverse_lazy('monitor_sendmail_success')

    def get_context_data(self, **kwargs):
        context = super().get_context_data(**kwargs)
        user = self.request.user
        right = user.monitor_right
        hqs = (qs['org_name'] for qs in right.authed_departments.values('org_name'))
        context['hqs'] = hqs
        return context

    def form_valid(self, form):
        data = form.cleaned_data
        to_emails =(qs['username'] for qs in Employee.objects.filter(team__hq=data['target_hq']).values('username'))
        subject = data['title']
        message = render_to_string('site_monitor/mail_form.html', context={'content': data['content']})
        from_email = 'info@mobisdietlab.com'

        send_mail(
            subject=subject,
            message='',
            html_message=message,
            from_email=from_email,
            recipient_list=to_emails)

        return super().form_valid(form)

def sendmail_success(request):
    return render(request,'site_monitor/sendmail_success.html')