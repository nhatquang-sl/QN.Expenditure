export QNEDIR="D:"
if [[ $(uname) == "Darwin" ]]; then
    export QNEDIR="/Users/quang"
fi

source ${QNEDIR}/QN.Expenditure/scripts/qnapiclientgenerate.zsh
source ${QNEDIR}/QN.Expenditure/scripts/qnapistart.zsh
source ${QNEDIR}/QN.Expenditure/scripts/qndbinit.zsh
source ${QNEDIR}/QN.Expenditure/scripts/qndbremove.zsh
source ${QNEDIR}/QN.Expenditure/scripts/qndbupdate.zsh
source ${QNEDIR}/QN.Expenditure/scripts/qnuistart.zsh